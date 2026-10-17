using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// UAE AML/CFT/PF programme for every tenant that is in scope: financial institutions,
/// designated non-financial businesses and professions, and virtual-asset service providers.
/// Dealers in precious metals and stones use the same pack, plus the cash and supply-chain annex,
/// when a single or linked cash transaction reaches AED 55,000.
/// The wording is the workspace pack. It is not a copy of any one company's manual.
/// </summary>
public static class ErpAmlProgramme
{
    public const string Decree = "Federal Decree-Law No. 10 of 2025";
    public const string Resolution = "Cabinet Resolution No. 134 of 2025";
    public const string BeneficialOwnerDecision = "Cabinet Decision No. 109 of 2023";
    public const string FatfStatement = "FATF statements of 19 June 2026";
    public const string PackVersion = "2026-06-19";
    public const decimal CoveredCashAed = 55_000m;
    public const int RecordYears = 5;
    public const int FreezeHours = 24;
    public const int TfsReportBusinessDays = 5;
    public const int PartialMatchBusinessDays = 10;
    public const int DpmsrDiaryDays = 14;
    public const int HighRiskReviewMonths = 6;

    public static readonly ErpAmlCountry[] Countermeasures =
    [
        new("KP", "Democratic People's Republic of Korea", ErpAmlCountryPosture.Countermeasures),
        new("IR", "Iran", ErpAmlCountryPosture.Countermeasures),
    ];

    public static readonly ErpAmlCountry[] CallForAction =
    [
        new("MM", "Myanmar", ErpAmlCountryPosture.CallForAction),
    ];

    public static readonly ErpAmlCountry[] IncreasedMonitoring =
    [
        new("AO", "Angola", ErpAmlCountryPosture.IncreasedMonitoring),
        new("BO", "Bolivia", ErpAmlCountryPosture.IncreasedMonitoring),
        new("BA", "Bosnia and Herzegovina", ErpAmlCountryPosture.IncreasedMonitoring),
        new("BG", "Bulgaria", ErpAmlCountryPosture.IncreasedMonitoring),
        new("CM", "Cameroon", ErpAmlCountryPosture.IncreasedMonitoring),
        new("CI", "Côte d'Ivoire", ErpAmlCountryPosture.IncreasedMonitoring),
        new("CD", "Democratic Republic of the Congo", ErpAmlCountryPosture.IncreasedMonitoring),
        new("HT", "Haiti", ErpAmlCountryPosture.IncreasedMonitoring),
        new("IQ", "Iraq", ErpAmlCountryPosture.IncreasedMonitoring),
        new("KE", "Kenya", ErpAmlCountryPosture.IncreasedMonitoring),
        new("KW", "Kuwait", ErpAmlCountryPosture.IncreasedMonitoring),
        new("LA", "Lao People's Democratic Republic", ErpAmlCountryPosture.IncreasedMonitoring),
        new("LB", "Lebanon", ErpAmlCountryPosture.IncreasedMonitoring),
        new("MC", "Monaco", ErpAmlCountryPosture.IncreasedMonitoring),
        new("NP", "Nepal", ErpAmlCountryPosture.IncreasedMonitoring),
        new("PG", "Papua New Guinea", ErpAmlCountryPosture.IncreasedMonitoring),
        new("SS", "South Sudan", ErpAmlCountryPosture.IncreasedMonitoring),
        new("SY", "Syria", ErpAmlCountryPosture.IncreasedMonitoring),
        new("VE", "Venezuela", ErpAmlCountryPosture.IncreasedMonitoring),
        new("VN", "Vietnam", ErpAmlCountryPosture.IncreasedMonitoring),
        new("VG", "Virgin Islands (UK)", ErpAmlCountryPosture.IncreasedMonitoring),
        new("YE", "Yemen", ErpAmlCountryPosture.IncreasedMonitoring),
    ];

    public static ErpAmlCountryPosture Posture(string? country)
    {
        var code = Normalize(country);
        if (code.Length == 0)
        {
            return ErpAmlCountryPosture.Clear;
        }

        if (Countermeasures.Any(item => item.Code == code))
        {
            return ErpAmlCountryPosture.Countermeasures;
        }

        if (CallForAction.Any(item => item.Code == code))
        {
            return ErpAmlCountryPosture.CallForAction;
        }

        if (IncreasedMonitoring.Any(item => item.Code == code))
        {
            return ErpAmlCountryPosture.IncreasedMonitoring;
        }

        return ErpAmlCountryPosture.Clear;
    }

    public static string? InvoiceRestriction(string? country)
    {
        var code = Normalize(country);
        var match = Countermeasures.Concat(CallForAction).FirstOrDefault(item => item.Code == code);
        if (match is null)
        {
            return null;
        }

        return match.Posture == ErpAmlCountryPosture.Countermeasures
            ? "This invoice is refused. " + match.Name + " is subject to FATF countermeasures in the statement of 19 June 2026 and to United Nations sanctions implemented in the UAE. Do not make funds, goods, or services available."
            : "This invoice is held and is not posted. " + match.Name + " is a FATF high-risk jurisdiction subject to a call for action (19 June 2026). Complete enhanced due diligence and sanctions screening, and record the compliance officer's decision on the customer file.";
    }

    public static IEnumerable<string> InvoiceRestrictions(params string?[] countries)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var country in countries)
        {
            var message = InvoiceRestriction(country);
            if (message is not null && seen.Add(message))
            {
                yield return message;
            }
        }
    }

    public static string ScreenSummary(string? country)
    {
        var code = Normalize(country);
        if (code.Length == 0)
        {
            return "Enter a country code to screen the invoice.";
        }

        var restriction = InvoiceRestriction(code);
        if (restriction is not null)
        {
            return restriction;
        }

        var grey = IncreasedMonitoring.FirstOrDefault(item => item.Code == code);
        if (grey is not null)
        {
            return grey.Name + " is under FATF increased monitoring (19 June 2026). The invoice is not refused. Apply enhanced due diligence. Do not cut off the whole class of customers.";
        }

        return code + " is not on the FATF countermeasure, call-for-action, or increased-monitoring lists in this pack. Still screen the name against the UN consolidated list and the UAE local terrorist list before the invoice is posted.";
    }

    public static IReadOnlyList<ErpAmlDocument> Documents { get; } =
    [
        Doc("register", "Registration", "Every in-scope tenant",
            "Register before the first covered transaction.",
            [
                Sec("Who is in scope", "Financial institutions, virtual-asset service providers, and designated non-financial businesses and professions are in scope under " + Decree + " and " + Resolution + ". Dealers in precious metals and stones are in scope when they carry out a single cash transaction, or linked cash transactions, of AED 55,000 or more. Real-estate brokers and agents, auditors, and company-service providers keep this same pack. A tenant that is not yet in scope still keeps the file so it is ready."),
                Sec("Ministry and the Unit", "The Ministry of Economy and Tourism supervises dealers in precious metals and stones and other designated non-financial businesses. The Financial Intelligence Unit receives reports on goAML. The Executive Office for Control and Non-Proliferation maintains targeted-financial-sanctions alerts."),
                Sec("Register these four", "Appoint a compliance officer and a money-laundering reporting officer and have senior management approve them. Register the business on goAML. Register on the Executive Office Notification Alert System. Tell the supervisory authority that the officers are appointed and that the policies are in force."),
                Sec("What to keep in the file", "Trade licence, owners and senior managers, the officer appointment, the goAML registration number, the Executive Office registration, and the date the policies were approved. Update the file when an officer changes."),
            ]),
        Doc("governance", "Governance framework", "Every in-scope tenant",
            "Senior management owns the programme. The officers run it.",
            [
                Sec("Senior management", "Approve the policies, the risk assessment, and the officer appointments. Receive a report of alerts, reports filed, sanctions matches, and gaps. Give the officers enough authority and resources. Do not interfere with a report to the Unit."),
                Sec("Compliance officer", "Keep the policies current, train the staff, test that the controls work, and track remedial action. Report deficiencies to senior management."),
                Sec("Money-laundering reporting officer", "Receive internal suspicious reports, decide whether to file, file on goAML, and keep the reporting diary. The officer is the contact for the Ministry, the Unit, and the Executive Office."),
                Sec("Staff", "Identify the customer, watch for red flags, escalate before completing a suspicious transaction, and do not tell the customer that a report was made."),
                Sec("Discipline", "A breach of this pack, a failure to escalate, or tipping-off is a conduct matter for senior management. The law still applies to the person who committed the breach."),
            ]),
        Doc("policy", "AML, CFT, and proliferation-financing policy", "Every in-scope tenant",
            "One policy for money laundering, terrorist financing, and proliferation financing.",
            [
                Sec("Purpose", "Stop the business being used to launder money, finance terrorism, or finance the proliferation of arms. Apply a risk-based approach under " + Decree + " and " + Resolution + "."),
                Sec("Prohibitions", "Do not open or continue a relationship where the customer or beneficial owner cannot be identified. Do not deal with a confirmed sanctions match. Do not tip off. Do not keep anonymous accounts. Do not rely on a third party in a high-risk country to do the due diligence."),
                Sec("Risk-based controls", "Identify the business risks, write controls that match them, and review the controls when the business, the customers, or the law changes. Use Fetch AML legislation when the Ministry or the FATF publishes an update."),
                Sec("Group and branches", "The same policy applies to every branch in the State. A branch outside the State applies the stricter of the local law and this pack, and tells the compliance officer where the local law prevents that."),
                Sec("Ministry file", "The registration, this policy, the risk assessment, the officer appointments, the training record, and the reporting diary are the file the supervisor can ask for. For a dealer in precious metals and stones that supervisor is the Ministry of Economy and Tourism. Keep the file in this workspace, send it when the supervisor asks, and send it again when an officer or the risk assessment changes."),
            ]),
        Doc("cdd", "Customer due diligence and beneficial ownership", "Every in-scope tenant",
            "Identify the customer and the beneficial owner before the covered relationship or transaction.",
            [
                Sec("When", "Identify the customer when the relationship starts, when an occasional transaction reaches the covered line, when suspicion arises, and when earlier information is doubtful. For a dealer in precious metals and stones the covered cash line is AED 55,000, including linked cash transactions. Below that line the dealer still identifies the customer. A buy above AED 100,000 is inside the AED 55,000 line."),
                Sec("A natural person", "Full name, nationality, date of birth, residential address, and an original identity document (Emirates ID, passport, or national ID). The name on the invoice matches the document. Keep a copy. Below the covered line, identify. At or above the line, verify against the original and record the source of funds."),
                Sec("A legal person", "Legal name, legal form, constitutional documents, registered address, senior managers, and the tax registration number when the person is subject to corporate tax. Identify each beneficial owner at 25 percent or more: name, nationality, and identity. If no one owns 25 percent, identify the person who controls the entity, or the senior manager. Identify the person acting and the authority to act. " + BeneficialOwnerDecision + " is the transparency rule for beneficial owners."),
                Sec("Simplified and enhanced", "Simplified due diligence is only for a demonstrated low risk, and it never skips sanctions screening. Enhanced due diligence applies to a high-risk customer, a politically exposed person, a high-risk country, an unusual ownership structure, and a non-profit. Update a high-risk file at least every six months."),
                Sec("Refusal", "If the due diligence cannot be completed, do not open the relationship and do not post the invoice. Consider an internal suspicious report."),
            ]),
        Doc("cash", "Cash acceptance", "Dealers in precious metals and stones, and any tenant that takes cash",
            "Cash is a known risk in gold and precious metals. Count it, record it, and know where it came from.",
            [
                Sec("Maker and checker", "The cashier receives the cash. A second person reconciles the drawer the same day. A manager authorises voids and refunds. Cash is stored securely and deposited. The person who receives cash is not the only person who records it."),
                Sec("AED 55,000", "A single cash transaction, or linked cash transactions, of AED 55,000 or more needs a completed customer file, verification against the original identity document, and evidence of the source of funds that fits the customer's occupation or business. Bank slips, exchange receipts, or a bank statement are examples."),
                Sec("Below AED 20,000", "A UAE resident and a demonstrated low risk may take simplified due diligence below AED 20,000. Sanctions screening still happens. Suspicion cancels the simplification."),
                Sec("Structuring", "Several cash payments just under AED 55,000 are one risk. Escalate them. Do not suggest a split so that a report is avoided."),
                Sec("DPMSR", "A qualified precious-metals or precious-stones transaction is reported on the goAML dealers' report. This workspace diaries that report for 14 days from the transaction and the Fetch button is how the diary is checked against the current Ministry circular."),
            ]),
        Doc("pep", "Politically exposed persons", "Every in-scope tenant",
            "A politically exposed person is a customer, a beneficial owner, or a close associate.",
            [
                Sec("Who", Resolution + " includes a person entrusted with a prominent public function in the State or abroad, and a senior person in an international organisation. Family members and known close associates are included. The status continues after the person leaves office for as long as the risk remains."),
                Sec("What to do", "Find out whether the customer or beneficial owner is a politically exposed person. Take senior-management approval before the relationship. Establish the source of funds and source of wealth. Monitor the relationship more closely. Review at least every six months."),
                Sec("After they leave the role", "A decision to treat the person as no longer politically exposed is written down and approved. The earlier status stays on the file."),
            ]),
        Doc("red-flags", "Red-flag indicators", "Every in-scope tenant. Precious-metal indicators are marked.",
            "A red flag is a reason to look harder. It is not, by itself, proof of a crime.",
            [
                Sec("Customer", "The customer refuses identification, uses a third party with no clear reason, is a foreign national with no economic link to the purchase, or is nervous about ordinary questions. The ownership structure hides the beneficial owner."),
                Sec("Precious metals and stones", "Metal or stones are said to come from a country that does not produce them. The customer repeatedly sells jewellery back for cash in small amounts. The goods do not match the customer's trade. A new customer wants high-value bars with no business history."),
                Sec("Transaction", "A large cash amount has no credible source. The pattern sits just under AED 55,000. The customer buys with cash and asks for a refund to a different person or to a bank in another country. The price is far from the market."),
                Sec("Payment and sanctions", "Payment is by a bearer instrument or by a person who is not the customer. The name, date of birth, or nationality is close to a sanctions-list entry. The goods or the payment touch a countermeasure country."),
                Sec("What staff do", "Stop and escalate to the compliance officer before completing the transaction. Write down what was seen. Do not tell the customer that a report is being considered."),
            ]),
        Doc("risk", "Business risk assessment", "Every in-scope tenant",
            "Write the risks down, score them, and show how the controls reduce them.",
            [
                Sec("Factors", "Customers, countries, products, delivery channels, and, for a dealer, the cash market in gold and stones. A politically exposed person, a high-risk country, and a complex company raise the score. A long-standing UAE resident buying a small finished item can be lower. Read the UAE National Risk Assessment into the file, including the gold-sector findings where the tenant deals in precious metals or stones. A product that is portable and bought with cash scores higher. A courier delivery, or a relationship that never meets the customer, scores higher than a counter sale."),
                Sec("Method", "Score inherent risk before controls, name the control, then score what remains. A residual high risk needs enhanced due diligence or a decision not to offer the product. Review the assessment when the business changes and at least once a year. Senior management approves it."),
                Sec("Use", "The assessment drives the cash limit, the review dates on customer files, and which products need a compliance sign-off. Keep the working papers for five years."),
            ]),
        Doc("str", "Suspicious transaction reporting", "Every in-scope tenant",
            "Report suspicion to the Unit on goAML. Do not tip off.",
            [
                Sec("Internal report", "Any member of staff who has a suspicion tells the money-laundering reporting officer at once, in writing, and does not complete the transaction unless stopping it would tip off the customer. The officer records the time the report arrived."),
                Sec("Filing", "If the officer has reasonable grounds, the officer files a suspicious transaction report on goAML without delay. The report states the customer, the transaction, the suspicion, and the documents held. A decision not to file is written down with the reason."),
                Sec("No tipping-off", "Do not tell the customer, the beneficial owner, or an outsider that a report was made or that an investigation is underway. The prohibition continues after the report."),
                Sec("After the report", "Follow any instruction from the Unit. Keep the report, the internal note, and the customer file for five years after the relationship ends or the transaction is completed."),
                Sec("The report file", "Keep the suspicious-report register apart from the customer file. Staff who serve the customer do not see that a report was filed. Only the reporting officer opens that register. An internal report is made to that officer, not to colleagues and not to the customer."),
            ]),
        Doc("tfs", "Targeted financial sanctions", "Every in-scope tenant",
            "Screen, freeze or reject, and report. This is separate from the suspicious-transaction report.",
            [
                Sec("Lists", "Screen customers, beneficial owners, directors, and counterparties, and any entity they own or control, against the United Nations Security Council consolidated list and the UAE local terrorist list issued under Cabinet Resolution No. 74 of 2020. Screen before the relationship, before a covered transaction, and again immediately when either list changes. Ministry of Economy and Tourism Circular No. 7 of 2025 restates this duty for designated non-financial businesses."),
                Sec("Freeze and reject", "A confirmed match on an existing customer: freeze the funds, assets, and services without delay, within 24 hours, including property of an entity owned or controlled by the listed person, and do not tell the customer first. A confirmed match on a new customer: reject the transaction. A partial match: suspend the transaction. If the identity documents do not arrive within 10 business days, reject it."),
                Sec("Reports", "File a confirmed-name-match report or a partial-name-match report on goAML to the Executive Office and the supervisory authority within five business days of the freeze, suspension, or rejection. Keep every screening result, including false positives, for five years. A person affected by a freeze uses the Executive Office grievance channel. The business does not lift a freeze on its own."),
                Sec("Countries are not a substitute for names", "A country list does not replace name screening. Most sanctions designate persons, not every resident of a country. Countermeasure countries are refused on the invoice as well."),
            ]),
        Doc("supply", "Supply chain and precious metals", "Dealers in precious metals and stones",
            "Know where the metal and the stones came from.",
            [
                Sec("Origin", "Record the supplier, the country of origin, and the description of the metal or stone. A claim that gold came from a country with no production is a red flag. Keep the supplier's own due-diligence file."),
                Sec("Supplier file", "The supplier file follows the legal-person due diligence. A purchase at or above AED 55,000, including a purchase above AED 100,000, needs the complete file before it is saved. Linked purchases on the same document are added together."),
                Sec("Conflict-affected minerals", "Follow the OECD Due Diligence Guidance for responsible supply chains of minerals from conflict-affected and high-risk areas. Stop the purchase where there is a reasonable risk of serious human-rights abuses or of support to a non-state armed group. Do not disguise the origin of metal, and do not bribe to hide taxes or royalties. Rough diamonds need a Kimberley Process certificate."),
                Sec("Conflict and proliferation", "Do not buy from a supplier who will not name the refinery or the mine when the risk is high. Do not buy from a countermeasure country. Escalate a supply chain that touches a high-risk jurisdiction."),
            ]),
        Doc("reports", "Reporting diary", "Every in-scope tenant",
            "These are the reports the Ministry, the Unit, and the Executive Office expect.",
            [
                Sec("As it happens", "Suspicious transaction report: without delay, on goAML. Confirmed or partial sanctions match: freeze or reject within 24 hours, then report within five business days. A new sanctions-list update: screen immediately."),
                Sec("Dealers", "Dealers in precious metals and stones file the dealers' report on goAML for a qualified transaction. This workspace diaries 14 days. Real-estate brokers file the real-estate activity report when their supervisor requires it. The same diary shows that duty."),
                Sec("To the Ministry", "Keep the risk assessment, the officer appointments, and the policy approvals ready for the Ministry of Economy and Tourism. Send them when the supervisor asks, and when an officer or the business risk changes. Do not wait for a visit to assemble the file."),
                Sec("Every year", "Senior management receives the annual compliance report: customers on file, enhanced files, alerts, reports filed, sanctions matches, training done, and gaps. The risk assessment is reviewed. Staff are trained again."),
            ]),
        Doc("training", "Training, screening, and review", "Every in-scope tenant",
            "People who take cash or post invoices must know the red flags.",
            [
                Sec("Staff screening", "Screen new staff before they handle customers or cash. Repeat the screen when a person moves into a role that can approve a high-risk customer."),
                Sec("Training", "Train at induction and at least once a year, and again when this pack or the law changes. Cover due diligence, cash, red flags, sanctions, reporting, and tipping-off. Keep the attendance record."),
                Sec("Independent review", "Someone who does not file the reports tests, at least once a year, whether the files, the screening, and the diary match this pack. Senior management receives the findings and the date each finding is closed."),
                Sec("Records", "Keep policies, risk assessments, customer files, screening results, reports, and training records for five years after the relationship ends or the transaction is completed, and longer if a case is still open."),
            ]),
    ];

    public static ErpAmlDocument? Find(string? code)
    {
        var key = (code ?? string.Empty).Trim().ToLowerInvariant();
        return Documents.FirstOrDefault(item => item.Code == key);
    }

    public static IReadOnlyList<ErpAmlStep> Steps { get; } =
    [
        new("1", "Register", "Appoint the officers. Register on goAML and the Executive Office. Tell the Ministry."),
        new("2", "Assess", "Write the business risk assessment and approve the policies."),
        new("3", "Identify", "Identify the customer and the beneficial owner. Screen the sanctions lists."),
        new("4", "Transact", "Take cash with a maker and a checker. Refuse a countermeasure country."),
        new("5", "Report", "File suspicion without delay. File sanctions matches within five business days. Diary the dealers' report."),
        new("6", "Keep", "Retain the file for five years. Train every year. Fetch the law when it changes."),
    ];

    public static IReadOnlyList<ErpAmlTimeFrame> TimeFrames { get; } =
    [
        new("Suspicious transaction report", "Suspicion arises", "Without delay, on goAML"),
        new("Freeze", "Confirmed match on an existing customer", "Within 24 hours. Do not tell the customer first"),
        new("Sanctions match report", "The freeze, suspension, or rejection", "Within five business days"),
        new("Partial-match identity", "A partial name match", "10 business days, then reject if the documents have not arrived"),
        new("Dealers' report", "A qualified precious-metals or precious-stones cash transaction", "Diary of 14 days. Fetch checks the current Ministry circular"),
        new("High-risk and politically exposed person review", "The last completed review", "At least every six months"),
        new("Annual review", "The last approval of the risk assessment and the training", "Once a year"),
        new("Ministry file", "A supervisor request, an officer change, or a change in risk", "Send the pack that is current on that date"),
        new("Records", "The relationship ends or the transaction is completed", "Five years, and longer while a case is open"),
    ];

    public static IReadOnlyList<ErpAmlClock> Clocks(DateTime utc)
    {
        var prepared = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return
        [
            new("Prepared", prepared.ToString("dd MMMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)),
            new("Legislation pack", PackVersion + " · " + Decree + " · " + Resolution),
            new("Dealers' report if the cash transaction is now", prepared.AddDays(DpmsrDiaryDays).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)),
            new("Sanctions report if the freeze starts now", AddBusinessDays(prepared, TfsReportBusinessDays).ToString("dd MMMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)),
            new("Identity documents if the partial match is now", AddBusinessDays(prepared, PartialMatchBusinessDays).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)),
            new("High-risk and PEP review if completed now", prepared.AddMonths(HighRiskReviewMonths).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)),
            new("Annual review if approved now", prepared.AddYears(1).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)),
            new("Record retention if the relationship ended now", prepared.AddYears(RecordYears).ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)),
        ];
    }

    public static DateTime AddBusinessDays(DateTime utc, int days)
    {
        var date = utc;
        var left = Math.Max(0, days);
        while (left > 0)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                left--;
            }
        }

        return date;
    }

    public static readonly ErpAmlOfficialSource[] OfficialSources =
    [
        new("FATF black and grey lists", "https://www.fatf-gafi.org/en/countries/black-and-grey-lists.html", "Iran"),
    ];

    private static string Normalize(string? country)
    {
        var code = (country ?? string.Empty).Trim().ToUpperInvariant();
        return code is "ARE" or "UAE" ? "AE" : code;
    }

    private static ErpAmlDocument Doc(string code, string title, string appliesTo, string lead, ErpAmlSection[] sections)
        => new(code, title, appliesTo, lead, sections);

    private static ErpAmlSection Sec(string heading, string body) => new(heading, body);
}

public enum ErpAmlCountryPosture
{
    Clear,
    IncreasedMonitoring,
    CallForAction,
    Countermeasures,
}

public sealed record ErpAmlCountry(string Code, string Name, ErpAmlCountryPosture Posture);

public sealed record ErpAmlSection(string Heading, string Body);

public sealed record ErpAmlDocument(
    string Code,
    string Title,
    string AppliesTo,
    string Lead,
    IReadOnlyList<ErpAmlSection> Sections);

public sealed record ErpAmlStep(string Number, string Title, string Body);

public sealed record ErpAmlOfficialSource(string Label, string Url, string Marker);

public sealed record ErpAmlTimeFrame(string Duty, string Clock, string Due);

public sealed record ErpAmlClock(string Label, string When);
