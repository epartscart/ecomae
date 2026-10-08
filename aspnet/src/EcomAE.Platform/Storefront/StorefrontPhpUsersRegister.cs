using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/users/register.php</c>: the registration form post. In one transaction: the captcha, agreement,
/// contact, IP and enhanced-field checks, the <c>users</c> row (or the SMS-code <c>simple_register</c> account), the
/// registration fields, the registered-customer group, the trade account and the buyer profile, then the
/// <c>reg_email_confirm</c> / <c>reg_phone_confirm</c> send. After the commit, <c>reg_notify_admin</c> to the staff and
/// the page text. Any refusal rolls back and goes to <c>{lang}/?error_message=</c>.
/// </summary>
public static partial class StorefrontPhpAjax
{
    public const string UsersRegisterPath = "/users/register";

    private static readonly string[] EnhancedOnlyCategories = ["business", "einvoice", "kyc_aml", "documents", "identity"];

    private static readonly string[] ProfileUserColumns =
    [
        "email", "email_confirmed", "email_code_send_lock_expired", "phone", "phone_confirmed", "phone_code_send_lock_expired", "reg_variant",
    ];

    public sealed record UsersRegisterRequest(
        IReadOnlyDictionary<string, string> Post,
        IReadOnlyDictionary<string, EpcRegistrationUpload> Files,
        IReadOnlyDictionary<string, string> Cookies,
        string RemoteIp,
        string UserAgent,
        string LangHref,
        string WebRoot);

    /// <summary>Either <see cref="Location"/> (the PHP <c>location=</c> after a refusal) or the page <see cref="Html"/>.</summary>
    public sealed record UsersRegisterOutcome(string? Location, string Html, long UserId);

    private sealed class RegisterRefusal(string? messageId, string suffix = "", string? literal = null) : Exception(literal ?? messageId)
    {
        public string? MessageId { get; } = messageId;

        public string Suffix { get; } = suffix;

        public string? Literal { get; } = literal;
    }

    /// <summary>
    /// Runs <c>register.php</c> on <paramref name="connection"/>. <paramref name="openSeparateConnection"/> gives the
    /// connection the confirmation send reads its settings on while the registration transaction is open.
    /// </summary>
    public static async Task<UsersRegisterOutcome> UsersRegisterAsync(
        DbConnection connection,
        Func<CancellationToken, Task<DbConnection>> openSeparateConnection,
        IStorefrontNotifyDispatcher? dispatcher,
        IReadOnlyDictionary<string, string> config,
        UsersRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var post = request.Post;
        string? P(string key) => post.TryGetValue(key, out var value) ? value : null;
        string C(string key) => config.TryGetValue(key, out var value) ? value : string.Empty;
        var simple = post.ContainsKey("simple_register");
        var langHref = request.LangHref;
        var langNoSlash = langHref.TrimStart('/');
        var translator = new StorefrontPhpTranslator(connection, langNoSlash.Length > 0 ? langNoSlash : "en");
        var secret = C("secret_succession");

        var regContact = EpcEinvoiceBuyer.PhpTrim(P("reg_contact"));
        var regContactType = P("reg_contact_type") ?? string.Empty;
        var customerType = P("epc_customer_type") ?? "retail";
        long userId = 0;
        string activationCode = string.Empty;
        var confirmEmailFailed = false;

        var confirmLinkText = await translator.TextAsync(4696, cancellationToken).ConfigureAwait(false);
        var regFields = await RowsAsync(connection, null, "SELECT * FROM `reg_fields` WHERE `main_flag` = 0", cancellationToken).ConfigureAwait(false);
        var captions = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (simple)
        {
            foreach (var field in regFields)
            {
                var caption = field.GetValueOrDefault("caption") ?? string.Empty;
                captions[caption] = caption.Length == 0 ? string.Empty : await translator.RawAsync(caption, cancellationToken).ConfigureAwait(false);
            }
        }

        var signedIn = await CookieUserIdAsync(connection, request.Cookies.GetValueOrDefault("session"), request.Cookies.GetValueOrDefault("u_id"), cancellationToken).ConfigureAwait(false);
        if (signedIn == 0)
        {
            await EpcEinvoiceBuyer.EnsureSchemaAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = signedIn == 0 ? await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false) : null;
        try
        {
            if (signedIn != 0)
            {
                throw new RegisterRefusal("4740");
            }

            if (!simple && !StorefrontSmsHandlers.LooseEquals(LegacyPasswordVerifier.Md5Hex(P("capcha_input") ?? string.Empty), request.Cookies.GetValueOrDefault("captcha") ?? string.Empty))
            {
                throw new RegisterRefusal("4041");
            }

            if (!simple && request.Cookies.GetValueOrDefault("users_agreement") != "yes")
            {
                throw new RegisterRefusal("4745");
            }

            string column;
            if (regContactType == "phone")
            {
                column = "phone";
            }
            else if (regContactType == "email")
            {
                column = "email";
            }
            else
            {
                throw new RegisterRefusal("2122", " 1.1");
            }

            var regexp = await ErpDb.StringAsync(connection, transaction, ErpDb.Positional("SELECT `regexp` FROM `reg_fields` WHERE `name` = ?"), cancellationToken, regContactType).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(regexp) && !PregWholeMatch(regexp, regContact))
            {
                throw new RegisterRefusal("2122", " 1.2");
            }

            var storedContact = PhpHtmlEntities.Encode(regContact);
            var taken = await ErpDb.LongAsync(connection, transaction, ErpDb.Positional("SELECT COUNT(*) FROM `users` WHERE `" + column + "` = ?"), cancellationToken, storedContact).ConfigureAwait(false);
            if (taken != 0)
            {
                if (simple)
                {
                    await transaction!.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new UsersRegisterOutcome(null, AuthenticateFormHtml(post, "            ", "                "), 0);
                }

                throw new RegisterRefusal("2122", " 1.3");
            }

            if (request.RemoteIp.Length == 0)
            {
                throw new RegisterRefusal("2122", " 2.1");
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fromIp = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT COUNT(*) FROM `users` WHERE `ip_address` = ? AND `time_registered` > ? AND `email_confirmed` = ? AND `phone_confirmed` = ?"),
                cancellationToken,
                request.RemoteIp,
                now - 86400,
                0,
                0).ConfigureAwait(false);
            if (fromIp > 0)
            {
                throw new RegisterRefusal("4746");
            }

            try
            {
                EpcRegistrationEnhanced.ValidateEnhancedFields(post, customerType, request.Files);
                EpcRegistrationEnhanced.ValidateUaeFields(post);
            }
            catch (EpcRegistrationException e)
            {
                throw new RegisterRefusal(null, literal: e.Message);
            }

            activationCode = regContactType == "email"
                ? LegacyPasswordVerifier.Md5Hex(LegacyPasswordVerifier.Md5Hex(regContact) + LegacyPasswordVerifier.Md5Hex(secret))
                : Random.Shared.Next(100000, 1000000).ToString(CultureInfo.InvariantCulture);

            var regVariant = P("reg_variant");
            if (await ErpDb.LongAsync(connection, transaction, ErpDb.Positional("SELECT COUNT(*) FROM `reg_variants` WHERE `id` = ?"), cancellationToken, regVariant).ConfigureAwait(false) != 1)
            {
                throw new RegisterRefusal("2122", " 5.1");
            }

            if (!simple)
            {
                try
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("INSERT INTO `users` (`" + column + "`, `reg_variant`, `password`, `" + column + "_code`, `time_registered`, `" + column + "_code_expired`, `unlocked`) VALUES (?, ?, ?, ?, ?, ?, ?)"),
                        cancellationToken,
                        storedContact,
                        regVariant,
                        LegacyPasswordVerifier.Md5Hex((P("password") ?? string.Empty) + secret),
                        activationCode,
                        now,
                        now + 1800,
                        1).ConfigureAwait(false);
                }
                catch (DbException)
                {
                    throw new RegisterRefusal("3912");
                }

                userId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                userId = await SimpleRegisterUserAsync(connection, transaction!, request, column, storedContact, regVariant, secret, now, cancellationToken).ConfigureAwait(false);
            }

            var earlyType = post.TryGetValue("epc_customer_type", out var rawType) ? AsciiLower(EpcEinvoiceBuyer.PhpTrim(rawType)) : "retail";
            foreach (var field in regFields)
            {
                var name = field.GetValueOrDefault("name") ?? string.Empty;
                var showFor = JsonArrayValues(field.GetValueOrDefault("show_for"));
                if (!simple)
                {
                    if (showFor is null || (!showFor.Any(v => LooseEqualsString(v, regVariant)) && !showFor.Any(v => LooseEqualsLong(v, PhpIntCast(regVariant)))))
                    {
                        continue;
                    }

                    var widget = field.GetValueOrDefault("widget_type") ?? "text";
                    var category = field.GetValueOrDefault("field_category") ?? string.Empty;
                    if (widget == "file")
                    {
                        continue;
                    }

                    if (earlyType != "wholesale"
                        && (EnhancedOnlyCategories.Contains(category, StringComparer.Ordinal) || name.StartsWith("epc_", StringComparison.Ordinal) || name is "company_name" or "patronymic"))
                    {
                        continue;
                    }

                    await InsertProfileAsync(name, PhpHtmlEntities.Encode(P(name) ?? string.Empty)).ConfigureAwait(false);
                }
                else if (showFor is not null && showFor.Any(v => LooseEqualsString(v, regVariant)))
                {
                    await InsertProfileAsync(name, captions.GetValueOrDefault(field.GetValueOrDefault("caption") ?? string.Empty)).ConfigureAwait(false);
                }
            }

            var groups = await RowsAsync(connection, transaction, "SELECT * FROM `groups` WHERE `for_registrated` = 1", cancellationToken).ConfigureAwait(false);
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("INSERT INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)"),
                    cancellationToken,
                    userId,
                    groups.Count > 0 ? groups[0].GetValueOrDefault("id") : null).ConfigureAwait(false);
            }
            catch (DbException)
            {
                throw new RegisterRefusal("4747");
            }

            await EpcCustomerTrade.SaveRegistrationAsync(connection, transaction, userId, customerType, cancellationToken).ConfigureAwait(false);
            try
            {
                await EpcRegistrationEnhanced.SaveUaeBuyerProfileAsync(connection, transaction, request.WebRoot, userId, post, request.Files, regContact, regContactType, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (EpcRegistrationException e)
            {
                throw new RegisterRefusal(null, literal: e.Message);
            }

            if (!simple)
            {
                var userIdText = userId.ToString(CultureInfo.InvariantCulture);
                var email = regContactType == "email";
                var vars = email
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["site_name"] = C("site_name"),
                        ["email_confirm_href"] = "<a style='text-decoration: underline; color: #0000ee;' target='_blank' href='" + C("domain_path") + langNoSlash
                            + "/users/confirm_contact?code=" + activationCode + "&u_id=" + userIdText + "&type=email'>" + confirmLinkText + "</a>",
                    }
                    : new Dictionary<string, string>(StringComparer.Ordinal) { ["phone_confirm_code"] = activationCode };
                var person = email ? StorefrontNotifyPerson.Direct(regContact) : StorefrontNotifyPerson.Direct(string.Empty, regContact);
                StorefrontNotifyAnswer? answer = null;
                if (dispatcher is not null)
                {
                    await using var separate = await openSeparateConnection(cancellationToken).ConfigureAwait(false);
                    answer = await dispatcher.SendAsync(separate, email ? "reg_email_confirm" : "reg_phone_confirm", vars, [person], cancellationToken).ConfigureAwait(false);
                }

                var sent = answer is { Found: true, Persons.Count: > 0 }
                    && (email ? answer.Persons[0].TriedToSend && answer.Persons[0].Status : answer.Persons[0].Sms.Status);
                if (!sent)
                {
                    if (C("registration_continue_if_confirm_email_fails") == "1" && email)
                    {
                        confirmEmailFailed = true;
                    }
                    else
                    {
                        throw new RegisterRefusal(answer is { Found: true } ? "4698" : "4697");
                    }
                }
            }

            await transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is RegisterRefusal or DbException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            var message = e is RegisterRefusal refusal
                ? refusal.Literal ?? await translator.TextAsync(refusal.MessageId, cancellationToken).ConfigureAwait(false) + refusal.Suffix
                : e.Message;
            return new UsersRegisterOutcome(langHref + "/?error_message=" + OAuthStart.PhpUrlEncode(message), string.Empty, 0);
        }

        try
        {
            await NotifyAdminOfRegistrationAsync(connection, dispatcher, translator, config, request, userId, regContact, customerType, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is DbException or InvalidOperationException or IOException or JsonException)
        {
        }

        if (simple)
        {
            return new UsersRegisterOutcome(null, AuthenticateFormHtml(post, "    ", "        "), userId);
        }

        return new UsersRegisterOutcome(null, await ResultHtmlAsync(connection, translator, config, regContactType, customerType, userId, activationCode, confirmEmailFailed, langNoSlash, cancellationToken).ConfigureAwait(false), userId);

        Task InsertProfileAsync(string key, string? value)
            => InsertRegistrationProfileAsync(connection, transaction!, userId, key, value, cancellationToken);
    }

    private static async Task InsertRegistrationProfileAsync(DbConnection connection, DbTransaction transaction, long userId, string key, string? value, CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)"),
                cancellationToken,
                userId,
                key,
                value).ConfigureAwait(false);
        }
        catch (DbException)
        {
            throw new RegisterRefusal("3913");
        }
    }

    /// <summary>The <c>simple_register</c> branch: the session's SMS code (attempts, expiry), then an account with a generated password.</summary>
    private static async Task<long> SimpleRegisterUserAsync(
        DbConnection connection,
        DbTransaction transaction,
        UsersRegisterRequest request,
        string column,
        string storedContact,
        string? regVariant,
        string secret,
        long now,
        CancellationToken cancellationToken)
    {
        var sessions = await RowsAsync(
            connection,
            transaction,
            "SELECT * FROM `sessions` WHERE `session` = ? AND `user_id` = ?",
            cancellationToken,
            request.Cookies.GetValueOrDefault("session"),
            request.Cookies.GetValueOrDefault("u_id")).ConfigureAwait(false);
        var session = sessions.Count > 0 ? sessions[0] : new Dictionary<string, string?>(StringComparer.Ordinal);
        var attempts = PhpIntCast(session.GetValueOrDefault("2fa_attempts"));
        if (attempts < 1)
        {
            throw new RegisterRefusal("4003");
        }

        var code = session.GetValueOrDefault("2fa_code");
        if (code is null || !request.Post.TryGetValue("code", out var posted) || code != posted)
        {
            attempts--;
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `sessions` SET `2fa_attempts` = ? WHERE `session` = ?"),
                cancellationToken,
                attempts,
                session.GetValueOrDefault("session")).ConfigureAwait(false);
            throw new RegisterRefusal("5643", ": " + attempts.ToString(CultureInfo.InvariantCulture) + ".");
        }

        if (FaCodeExpired(session.GetValueOrDefault("data"), now))
        {
            throw new RegisterRefusal("5642");
        }

        var password = LegacyPasswordVerifier.Md5Hex(Random.Shared.Next(1000, 1_000_000_001).ToString(CultureInfo.InvariantCulture) + secret);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `users` (`" + column + "`, `reg_variant`, `password`, `" + column + "_confirmed`, `time_registered`, `unlocked`) VALUES (?, ?, ?, ?, ?, ?)"),
                cancellationToken,
                storedContact,
                regVariant,
                LegacyPasswordVerifier.Md5Hex(password + secret),
                1,
                now,
                1).ConfigureAwait(false);
        }
        catch (DbException)
        {
            throw new RegisterRefusal("3912");
        }

        return await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>$session_data["expireFaCode"] &lt; time()</c> for the decoded session data.</summary>
    private static bool FaCodeExpired(string? data, long now)
    {
        JsonElement expire;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrEmpty(data) ? "null" : data);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("expireFaCode", out var value))
            {
                return true;
            }

            expire = value.Clone();
        }
        catch (JsonException)
        {
            return true;
        }

        return expire.ValueKind switch
        {
            JsonValueKind.Number => expire.GetDouble() < now,
            JsonValueKind.String when PhpNumericString(expire.GetString()!, out var number) => number < now,
            JsonValueKind.String => string.CompareOrdinal(expire.GetString(), now.ToString(CultureInfo.InvariantCulture)) < 0,
            JsonValueKind.True => false,
            _ => true,
        };
    }

    /// <summary>The <c>reg_notify_admin</c> send after the commit: the profile table, the profile button and the staff persons.</summary>
    private static async Task NotifyAdminOfRegistrationAsync(
        DbConnection connection,
        IStorefrontNotifyDispatcher? dispatcher,
        StorefrontPhpTranslator translator,
        IReadOnlyDictionary<string, string> config,
        UsersRegisterRequest request,
        long userId,
        string regContact,
        string customerType,
        CancellationToken cancellationToken)
    {
        string C(string key) => config.TryGetValue(key, out var value) ? value : string.Empty;
        var mainColor = "#799658";
        var template = await ErpDb.StringAsync(connection, null, "SELECT `data_value` FROM `templates` WHERE `is_frontend` = 1 AND `current` = 1 LIMIT 1", cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(template))
        {
            try
            {
                using var doc = JsonDocument.Parse(template);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("main_color", out var color)
                    && !EpcEinvoiceBuyer.PhpEmpty(color.ValueKind == JsonValueKind.String ? color.GetString() : color.ValueKind is JsonValueKind.Null or JsonValueKind.False ? null : color.GetRawText()))
                {
                    mainColor = color.ValueKind == JsonValueKind.String ? color.GetString()! : color.GetRawText();
                }
            }
            catch (JsonException)
            {
            }
        }

        var (profile, profileGroups) = await RegisteredUserProfileAsync(connection, userId, cancellationToken).ConfigureAwait(false);
        var table = new StringBuilder();
        table.Append("<h4>").Append(await translator.TextAsync(4748, cancellationToken).ConfigureAwait(false)).Append("</h4>");
        table.Append("<table cellspacing='0' style='border-collapse: collapse; margin-top: 15px;'>");
        foreach (var field in await RowsAsync(connection, null, "SELECT * FROM `reg_fields` WHERE `main_flag` = 0 ORDER BY `order` ASC", cancellationToken).ConfigureAwait(false))
        {
            if (profile.TryGetValue(field.GetValueOrDefault("name") ?? string.Empty, out var value) && value is not null)
            {
                table.Append("<tr><td>").Append(field.GetValueOrDefault("caption")).Append("</td><td>").Append(value).Append("</td></tr>");
            }
        }

        if (!EpcEinvoiceBuyer.PhpEmpty(profile.GetValueOrDefault("email")))
        {
            table.Append("<tr><td>E-mail</td><td>").Append(profile["email"]).Append("</td></tr>");
        }

        if (!EpcEinvoiceBuyer.PhpEmpty(profile.GetValueOrDefault("phone")))
        {
            table.Append("<tr><td>").Append(await translator.TextAsync(1312, cancellationToken).ConfigureAwait(false)).Append("</td><td>").Append(profile["phone"]).Append("</td></tr>");
        }

        var firstGroup = profileGroups.Count > 0 ? profileGroups[0] : null;
        foreach (var group in await RowsAsync(connection, null, "SELECT * FROM `groups`", cancellationToken).ConfigureAwait(false))
        {
            if (firstGroup is not null && StorefrontSmsHandlers.LooseEquals(firstGroup, group.GetValueOrDefault("id") ?? string.Empty))
            {
                table.Append("<tr><td>").Append(await translator.TextAsync(3664, cancellationToken).ConfigureAwait(false)).Append("</td><td>").Append(group.GetValueOrDefault("value")).Append("</td></tr>");
            }
        }

        table.Append("</table>");
        var tableHtml = table.ToString();
        var userIdText = userId.ToString(CultureInfo.InvariantCulture);
        var userLink = "<a style=\"background: " + mainColor + "; color: #fff; text-decoration: none; padding: 7px 13px; font-size: 16px; border-radius: 5px; display: inline-block;\" href='"
            + C("domain_path") + C("backend_dir") + "/users/usermanager/user?user_id=" + userIdText + "'>" + await translator.TextAsync(3539, cancellationToken).ConfigureAwait(false) + "</a>";

        var admins = new List<StorefrontNotifyPerson>();
        var backend = await StorefrontOrderNotificationService.BackendGroupIdsAsync(connection, cancellationToken).ConfigureAwait(false);
        if (backend.Count > 0)
        {
            foreach (var row in await RowsAsync(
                connection,
                null,
                "SELECT DISTINCT `user_id` FROM `users_groups_bind` WHERE `group_id` IN (" + string.Join(",", backend.Select(g => g.ToString(CultureInfo.InvariantCulture))) + ")",
                cancellationToken).ConfigureAwait(false))
            {
                admins.Add(StorefrontNotifyPerson.User((int)PhpIntCast(row.GetValueOrDefault("user_id"))));
            }
        }

        var contactLabel = !EpcEinvoiceBuyer.PhpEmpty(profile.GetValueOrDefault("email")) ? profile["email"]! : regContact;
        var eventHtml = await AuthEventHtmlAsync(connection, translator, config, "New customer registration", userId, contactLabel, tableHtml, request.RemoteIp, request.UserAgent, cancellationToken).ConfigureAwait(false);
        var userProfile = new StringBuilder(tableHtml).Append(eventHtml);
        userProfile.Append("<p><strong>Requested account type:</strong> ").Append(HtmlSpecialChars(EpcCustomerTrade.CustomerTypeLabel(customerType))).Append("</p>");
        userProfile.Append(customerType == "retail" && await EpcCustomerTrade.IsApprovedAsync(connection, null, userId, cancellationToken).ConfigureAwait(false)
            ? "<p><strong>Trade approval:</strong> Auto-approved (retail).</p>"
            : "<p><strong>Trade approval:</strong> Pending — review in Control Panel → Users → Customer approvals.</p>");
        if (request.Post.TryGetValue("epc_uae_company", out var uaeCompany) && !EpcEinvoiceBuyer.PhpEmpty(uaeCompany))
        {
            userProfile.Append("<p><strong>UAE e-invoice:</strong> Buyer TRN/profile captured at registration.</p>");
        }

        var persons = await StaffPersonsWithAsync(connection, config, (int)userId, admins, cancellationToken).ConfigureAwait(false);
        if (dispatcher is null)
        {
            return;
        }

        await dispatcher.SendAsync(
            connection,
            "reg_notify_admin",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["user_id"] = userIdText,
                ["user_profile"] = userProfile.ToString(),
                ["user_link"] = userLink,
                ["event_html"] = eventHtml,
            },
            persons,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_staff_notify_persons($customer_id, 0, $extra)</c>: the admin inbox, the CRM manager, then the extra persons, users once each.</summary>
    public static async Task<IReadOnlyList<StorefrontNotifyPerson>> StaffPersonsWithAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> config,
        int customerId,
        IReadOnlyList<StorefrontNotifyPerson> extra,
        CancellationToken cancellationToken)
    {
        var adminEmail = await StorefrontOrderNotificationService.AdminEmailAsync(connection, config, cancellationToken).ConfigureAwait(false);
        var persons = new List<StorefrontNotifyPerson> { StorefrontNotifyPerson.Direct(adminEmail) };
        var seen = new HashSet<int>();
        var crm = await StorefrontOrderNotificationService.CrmUserIdAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
        var candidates = new List<StorefrontNotifyPerson>();
        if (crm > 0)
        {
            candidates.Add(StorefrontNotifyPerson.User(crm));
        }

        candidates.AddRange(extra);
        var admin = adminEmail.ToLowerInvariant();
        foreach (var person in candidates)
        {
            if (person.Type == StorefrontNotifyPerson.UserIdType)
            {
                if (person.UserId > 0 && seen.Add(person.UserId))
                {
                    persons.Add(person);
                }
            }
            else if (person.Type == StorefrontNotifyPerson.DirectContactType && person.Email.Length > 0)
            {
                var email = EpcEinvoiceBuyer.PhpTrim(person.Email).ToLowerInvariant();
                if (email.Length > 0 && email != admin)
                {
                    persons.Add(person);
                }
            }
        }

        return persons;
    }

    /// <summary>PHP <c>epc_build_auth_event_html()</c>: the event, contact, user, server time, IP, browser, extra and customer profile.</summary>
    public static async Task<string> AuthEventHtmlAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        IReadOnlyDictionary<string, string> config,
        string eventName,
        long userId,
        string contact,
        string extra,
        string remoteIp,
        string userAgent,
        CancellationToken cancellationToken)
    {
        var html = new StringBuilder("<div style=\"font-family:Calibri,Arial,sans-serif;font-size:14px;\">");
        html.Append("<p><strong>").Append(HtmlSpecialChars(eventName)).Append("</strong></p>");
        if (contact.Length > 0)
        {
            html.Append("<p>Contact: ").Append(HtmlSpecialChars(contact)).Append("</p>");
        }

        html.Append("<p>User ID: ").Append(userId.ToString(CultureInfo.InvariantCulture)).Append("</p>");
        html.Append("<p>Time: ").Append(StorefrontNotifyDispatcher.PlatformNow().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)).Append(" (server)</p>");
        if (remoteIp.Length > 0)
        {
            html.Append("<p>IP: ").Append(HtmlSpecialChars(remoteIp)).Append("</p>");
        }

        if (userAgent.Length > 0)
        {
            html.Append("<p style=\"font-size:12px;color:#666;\">Browser: ").Append(HtmlSpecialChars(userAgent)).Append("</p>");
        }

        html.Append(extra);
        if (userId > 0)
        {
            html.Append(await StorefrontOrderNotificationService.CustomerProfileHtmlAsync(connection, translator, (int)userId, null, config, cancellationToken).ConfigureAwait(false));
        }

        return html.Append("</div>").ToString();
    }

    /// <summary>PHP <c>DP_User::getUserProfileById()</c>: the account columns, the profile keys (later rows win) and the bound groups.</summary>
    private static async Task<(Dictionary<string, string?> Profile, List<string?> Groups)> RegisteredUserProfileAsync(DbConnection connection, long userId, CancellationToken cancellationToken)
    {
        var profile = new Dictionary<string, string?>(StringComparer.Ordinal) { ["user_id"] = userId.ToString(CultureInfo.InvariantCulture) };
        var users = await RowsAsync(connection, null, "SELECT * FROM `users` WHERE `user_id` = ?", cancellationToken, userId).ConfigureAwait(false);
        foreach (var key in ProfileUserColumns)
        {
            profile[key] = users.Count > 0 ? users[0].GetValueOrDefault(key) : null;
        }

        foreach (var row in await RowsAsync(connection, null, "SELECT * FROM `users_profiles` WHERE `user_id` = ?", cancellationToken, userId).ConfigureAwait(false))
        {
            profile[row.GetValueOrDefault("data_key") ?? string.Empty] = row.GetValueOrDefault("data_value");
        }

        var groups = (await RowsAsync(connection, null, "SELECT * FROM `users_groups_bind` WHERE `user_id` = ?", cancellationToken, userId).ConfigureAwait(false))
            .Select(r => r.GetValueOrDefault("group_id"))
            .ToList();
        return (profile, groups);
    }

    /// <summary>The page text after a registration: the e-mail (sent or not) or the SMS code form.</summary>
    private static async Task<string> ResultHtmlAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        IReadOnlyDictionary<string, string> config,
        string regContactType,
        string customerType,
        long userId,
        string activationCode,
        bool confirmEmailFailed,
        string langNoSlash,
        CancellationToken cancellationToken)
    {
        var userIdText = userId.ToString(CultureInfo.InvariantCulture);
        if (regContactType == "email")
        {
            if (confirmEmailFailed)
            {
                var url = (config.TryGetValue("domain_path", out var domain) ? domain : string.Empty) + langNoSlash + "/users/confirm_contact?code="
                    + OAuthStart.PhpUrlEncode(activationCode) + "&u_id=" + userIdText + "&type=email";
                return "<div class=\"alert alert-warning\" style=\"margin:15px 0;\">Account created, but the confirmation e-mail could not be sent (configure SMTP). Use this link to confirm your e-mail:</div>"
                    + "<p style=\"margin:15px 0; word-break:break-all;\"><a href=\"" + HtmlSpecialChars(url) + "\">" + HtmlSpecialChars(url) + "</a></p>";
            }

            var html = await translator.TextAsync(4749, cancellationToken).ConfigureAwait(false);
            return customerType == "retail" && await EpcCustomerTrade.IsApprovedAsync(connection, null, userId, cancellationToken).ConfigureAwait(false)
                ? html + "<div class=\"alert alert-success\" style=\"margin:15px 0;\">You registered as <strong>Retail</strong>. Your account is approved — confirm your e-mail, then sign in to browse and checkout. Contact us if you need a wholesale upgrade.</div>"
                : html + "<div class=\"alert alert-info\" style=\"margin:15px 0;\">You chose <strong>" + HtmlSpecialChars(EpcCustomerTrade.CustomerTypeLabel(customerType))
                    + "</strong> — subject to approval only. You can sign in and browse after confirming your contact. Checkout unlocks once a manager approves your wholesale account and assigns your dealing currency.</div>";
        }

        var codeLabel = await translator.TextAsync(4708, cancellationToken).ConfigureAwait(false);
        return "        " + await translator.TextAsync(4750, cancellationToken).ConfigureAwait(false) + ":\n"
            + "        <form method=\"GET\" action=\"/users/confirm_contact\">\n"
            + "            <input type=\"hidden\" name=\"u_id\" value=\"" + userIdText + "\" />\n"
            + "            <input type=\"hidden\" name=\"type\" value=\"phone\" />\n"
            + "            <div class=\"form-group\">\n"
            + "                <label for=\"\" class=\"col-sm-2 control-label\">" + codeLabel + "</label>\n"
            + "                <div class=\"col-sm-6\" style=\"padding:5px;\">\n"
            + "                  <input type=\"text\" class=\"form-control\" name=\"code\" id=\"code\" value=\"\" placeholder=\"" + codeLabel + "\">\n"
            + "                </div>\n"
            + "                <div class=\"col-sm-4\" style=\"padding:5px;\">\n"
            + "                    <button type=\"submit\">" + await translator.TextAsync(4521, cancellationToken).ConfigureAwait(false) + "</button>\n"
            + "                </div>\n"
            + "            </div>\n"
            + "        </form>\n"
            + "        ";
    }

    /// <summary>The <c>simple_register</c> hand-over to the sign-in post (the posted values HTML-escaped).</summary>
    private static string AuthenticateFormHtml(IReadOnlyDictionary<string, string> post, string indent, string inner)
    {
        string V(string key) => WebUtility.HtmlEncode(post.TryGetValue(key, out var value) ? value : string.Empty);
        return indent + "<form id=\"formAuthenticate\" action=\"/\" method=\"post\">\n"
            + inner + "<input type=\"hidden\" name=\"authentication\" value=\"true\">\n"
            + inner + "<input type=\"hidden\" name=\"auth_contact\" value=\"" + V("reg_contact") + "\">\n"
            + inner + "<input type=\"hidden\" name=\"auth_contact_type\" value=\"" + V("reg_contact_type") + "\">\n"
            + inner + "<input type=\"hidden\" name=\"code\" value=\"" + V("code") + "\">\n"
            + inner + "<input type=\"hidden\" name=\"csrf_guard_key\" value=\"" + V("csrf_guard_key") + "\">\n"
            + indent + "</form>\n"
            + indent + "<script>\n"
            + inner + "document.querySelector('#formAuthenticate').submit();\n"
            + indent + "</script>\n"
            + indent;
    }

    private static string HtmlSpecialChars(string? value) => HtmlEntitiesQuotes(value ?? string.Empty);

    private static string AsciiLower(string value)
        => string.Create(value.Length, value, (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = source[i] is >= 'A' and <= 'Z' ? (char)(source[i] + 32) : source[i];
            }
        });

    /// <summary>The values of a JSON array or object (<c>json_decode($x, true)</c> as an array), else <c>null</c>.</summary>
    private static List<JsonElement>? JsonArrayValues(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList(),
                JsonValueKind.Object => doc.RootElement.EnumerateObject().Select(p => p.Value.Clone()).ToList(),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>PHP 8 <c>$element == $value</c> for a posted string.</summary>
    private static bool LooseEqualsString(JsonElement element, string? value)
    {
        value ??= string.Empty;
        return element.ValueKind switch
        {
            JsonValueKind.String => StorefrontSmsHandlers.LooseEquals(element.GetString(), value),
            JsonValueKind.Number => PhpNumericString(value, out var number)
                ? number == element.GetDouble()
                : string.Equals(element.GetRawText(), value, StringComparison.Ordinal),
            JsonValueKind.True => value is not ("" or "0"),
            JsonValueKind.False => value is "" or "0",
            JsonValueKind.Null => value.Length == 0,
            _ => false,
        };
    }

    /// <summary>PHP 8 <c>$element == $value</c> for an integer.</summary>
    private static bool LooseEqualsLong(JsonElement element, long value)
        => element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDouble() == value,
            JsonValueKind.String => PhpNumericString(element.GetString()!, out var number)
                ? number == value
                : string.Equals(element.GetString(), value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
            JsonValueKind.True => value != 0,
            JsonValueKind.False or JsonValueKind.Null => value == 0,
            _ => false,
        };

    private static bool PhpNumericString(string value, out double number)
    {
        number = 0;
        var trimmed = value.Trim(' ', '\t', '\n', '\r', '\v', '\f');
        return trimmed.Length > 0
            && (char.IsAsciiDigit(trimmed[^1]) || trimmed[^1] == '.')
            && double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    /// <summary>PHP <c>(int)$value</c> for a string: leading whitespace, a sign and the leading digits.</summary>
    private static long PhpIntCast(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var span = value.AsSpan().TrimStart(" \t\n\r\v\f");
        var end = 0;
        if (end < span.Length && span[end] is '+' or '-')
        {
            end++;
        }

        while (end < span.Length && char.IsAsciiDigit(span[end]))
        {
            end++;
        }

        return long.TryParse(span[..end], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    private static async Task<List<Dictionary<string, string?>>> RowsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params object?[] args)
    {
        var rows = new List<Dictionary<string, string?>>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i) is bool flag ? (flag ? "1" : "0") : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            }

            rows.Add(row);
        }

        return rows;
    }
}
