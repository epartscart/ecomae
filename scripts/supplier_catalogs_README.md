Supplier catalogs store
=======================
One directory per catalog: <id>/catalog.pdf + <id>/catalog.json (page index).

Update a catalog to a newer edition (single-version policy — the new
import REPLACES the old directory; only the latest is kept):
  python3 scripts/import_supplier_catalog.py NEW.pdf <this-dir> --id <existing-id> --supplier <SUPPLIER>
  then rsync to prod: rsync -az <this-dir>/ root@<host>:/var/www/ecomae-aspnet/shared/supplier_catalogs/

Add a new supplier: same command with a new --id.
Remove a catalog: delete its <id> directory.
