# Future installs are ASP.NET Core only

New on-premises and hybrid customer installs use this pack (or the hybrid ASP.NET + sync pack). Do not add PHP files, PHP images, or PHP installer steps here.

Cloud production still keeps the PHP tree as fallback until `ReadyToRemovePhp=true` **and** a human `APPROVED_TO_REMOVE_PHP_FALLBACK`. Do not invent that gate. Do not delete PHP from GitHub or the live server from this pack.

If a change needs a new screen, API, or installer step, implement it in `aspnet/` and this directory. Do not create a `.php` counterpart.
