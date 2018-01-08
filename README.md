>   This is not a MediaWiki API client library. For such web API client library with Wikibase access support, see [CXuesong/WikiClientLibrary](https://github.com/CXuesong/WikiClientLibrary).

# WikibaseClientLite

This WIP repostiroy contains WikibaseClientLite, an attempt to simulate (a subset of) [Wikibase Client Lua API](https://www.mediawiki.org/wiki/Extension:Wikibase_Client/Lua) on MediaWiki sites without Wikibase extensions based on a Wikibase items dump. Especially the target MedaiWiki site may retrieve the item information from a LUA-table-based snapshot without direct depenency to Wikibase repository database or Wikibase client extension.

## WikibaseClientLite.ModuleExporter

A commandline interface that can generate [`mw.loadData`](https://www.mediawiki.org/wiki/Extension:Scribunto/Lua_reference_manual#mw.loadData)-compatible Lua data modules containing the Wikibase item information (labels, descriptions, aliases, claims, etc.) from an existing JSON dump of Wikibase entities exported with `extensions/Wikibase/repos/maintenance/dumpJson.php`. This will later enable retrieving of item information from Lua modules on the target site. This project is still under development.