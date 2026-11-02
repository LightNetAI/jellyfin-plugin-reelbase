<p align="center">
  <img alt="Reelbase" src="assets/icon.png" width="128" height="128"/>
</p>

<h1 align="center">Jellyfin Reelbase Plugin</h1>

<p align="center">
Metadata and artwork for music videos, from <a href="https://reelbase.me/">Reelbase</a>.
</p>

## About

[Reelbase](https://reelbase.me/) is a catalogue of music videos. This plugin turns it
into a Jellyfin metadata provider for the music video library type:

- **Music videos** - title, year, release date, duration, description, genres, artists, studios,
  and the cast and crew (director, producer, editor, director of photography, and whatever else
  the catalogue records), each with a link to their Reelbase page.
- **Artwork** - the video's cover art and a person's portrait.
- **Artists and people** - biographies, country, formed year and website, for the artists and
  crew that Jellyfin creates from the music videos.

It is a rewrite of the
[Jellyfin IMVDb plugin](https://github.com/jellyfin/jellyfin-plugin-imvdb) (GPLv3) against the
[Reelbase API](https://api.reelbase.me/api), and stays under the same licence.

## Installing

In Jellyfin, go to **Dashboard -> Plugins -> Repositories**, add this address:

```
https://lightnetai.github.io/jellyfin-plugin-reelbase/manifest.json
```

The raw file on GitHub works just as well:

```
https://raw.githubusercontent.com/LightNetAI/jellyfin-plugin-reelbase/main/manifest.json
```

Save, then open the **Catalog** tab, find **Reelbase** under *Music* and install it. Restart
Jellyfin when it asks.

The compiled plugin is committed in [dist](dist), so the repository works on its own: the
manifest points at the packaged zip next to it, not at a build server.

### Requirements

- Jellyfin 12 (the plugin targets ABI `12.0.0.0` and .NET 10).
- A Reelbase API key.

## Configuring

Create an API key on your Reelbase instance under **Account -> API keys** (keys are read-only by
default, which is all the plugin needs), then paste it into **Dashboard -> Plugins -> Reelbase**.

| Setting | Meaning |
| --- | --- |
| **Reelbase API key** | The bearer token every request carries. Without it no lookup is made at all. |
| **Reelbase address** | The instance to read from. Leave it at `https://api.reelbase.me` unless you run your own Reelbase. |
| **Cache duration (hours)** | How long a response is reused before it is fetched again. Defaults to 24. |

There is no setting for the request allowance, because the plugin does not decide it: the
figure comes from the API.

If you installed an earlier version and the **Reelbase address** still holds the host it used
to default to, clear the field and save once: that host was retired, and the plugin falls back
to the default above.

### About the request allowance

A Reelbase account gets a fixed number of API requests per UTC day, shared by every key on the
account, and a full metadata refresh spends about two requests per video (one search, one detail
lookup). On a 2,000 request day that is roughly 1000 videos on a fresh cache

**The plugin has no hard-coded limit and no setting for one.** It reads `X-RateLimit-Limit`,
`X-RateLimit-Remaining` and `X-RateLimit-Reset` **once, at the start of each run**, and keeps its
own count from there. Reading them on every response would keep overwriting the count with a
figure that also moves whenever the account is used elsewhere; counting locally makes the
plugin's own spending the only thing it tracks.

**It only spends 95% of the allowance.** The last 5% is left alone deliberately: on a 2,000
request day the plugin stops at 1950, so there is always room for new content, for a manual
lookup, or for anything else on the same key while a library refresh is grinding through the
backlog. The figure last reported is kept on disk, so restarting Jellyfin does not believe the
allowance is untouched.

On top of that:

- a response that carries records is cached, for 24 hours by default, so a rescan inside a day
  costs nothing; a lookup that found nothing is asked again, so a record added to Reelbase is
  picked up on the next try instead of the next day;
- when the allowance is spent, lookups stop for the rest of the day with one warning in the log
  instead of failing the scan or hammering the API;
- requests are spaced out, and an HTTP 429 is taken at its word: the plugin stops until the
  `Retry-After` it returns, waits it out inline when that is brief, and otherwise gives up the
  run rather than parking a scan until the window resets. The wait is remembered across a
  restart, so restarting Jellyfin does not spend another request rediscovering the refusal;
- if Reelbase stops answering - a 5xx, a refused key, a connection that never completes - ten
  failures in a row pause the lookups for two minutes instead of asking once per library item,
  and a nightly run gives up rather than grinding through thousands of failures.

### The nightly refresh

Because a library can be much larger than one day's allowance, the plugin registers a scheduled
task, **Refresh music video metadata from Reelbase**, which runs every 24 hours. It works through
the music video library **least recently refreshed first** and stops the moment the allowance is
spent. Jellyfin stamps each item with the time it was refreshed, which is what makes the task
resumable: tonight's items sort to the back tomorrow, and everything that was never reached stays
at the front, so a library of any size is covered over consecutive nights without repeating work.

You can watch it under **Dashboard -> Scheduled Tasks**, where it also has a **Run** button if you
would rather kick it off by hand. Progress and the number of items left are written to the log.

## Using it

1. Add a **Music videos** library to Jellyfin and point it at your files.
2. Name the files the usual way, `Artist - Title`, so the artist can be matched.
3. Refresh the library's metadata, or use **Identify** on an item to pick a record by hand.

The artist is linked as the metadata lands. Jellyfin shows a music video's artist through the
artist item it refers to, and it only creates those items after a **library scan**; refreshing
metadata alone leaves the name stored but unlinked, which is why the Artists field can look empty.
The plugin asks for the artist itself, so the link exists as soon as an item is refreshed.

The refresh dialog offers three choices, and only two of them reach a provider:

| Choice | What happens |
| --- | --- |
| Scan for new and updated files | The default. Jellyfin only looks at items whose file changed, so an item that has already been scanned is left alone and nothing is fetched. |
| Search for missing metadata | The provider runs and fills the fields that are empty. A name taken from the file name is kept. |
| Replace all metadata | The provider runs and its answer is applied, so a file named `aaliyah_-_try_again` becomes **Try Again**. Fields the record does not fill keep their existing values. |

To fix names in bulk, use **Replace all metadata** — on the item, on a folder, or on the whole
library. The nightly task below does the same thing for the whole library on a schedule.

Reelbase matches search terms as a single substring rather than word by word, so the item is
submitted as the file names it: one query, the whole name, artist and title together, and the API
decides whether the catalogue holds it. The results are then ranked by title, artist and year
instead of trusting the API's ordering. The artist is never asked for on its own, and the name is
not split up first.

The ``Artist - Title`` form is the one the API forgives punctuation for, so
``George Michael - Freedom `90`` finds ``Freedom! '90``, while a bare title is matched
literally and a file called just ``What I`ve Done.mkv`` has nothing to pair the title with.

Each search also carries the item's extension, `ext=mkv` for a file and `ext=null` for a folder
or a name with none, so the API can tell the two apart and record a miss on a file as missing
content. A dot inside the title is not an extension: ``Mr. Brightside`` and ``Song Vol. 2``
both go up as `ext=null`.

Reelbase only accepts certain extensions, and the list of them belongs to the site. The plugin
keeps a copy in `plugins/configurations/Reelbase/extensions.json`, beside its configuration, and
during a scan drops a file whose extension is not on that list **before** any request is made.
A scan never fetches the list: at startup, and again at the start of the nightly task, the plugin
asks `/api/v1/extensions/version` and downloads `/api/v1/extensions` only when that number has
moved on. A server that has never reached the API allows everything, because the API refuses what
it does not accept anyway, and a list that cannot be refreshed is kept rather than emptied.

## Building

```bash
dotnet build Jellyfin.Plugin.Reelbase/Jellyfin.Plugin.Reelbase.csproj -c Release
dotnet test tests/Jellyfin.Plugin.Reelbase.Tests/Jellyfin.Plugin.Reelbase.Tests.csproj
```

To produce the package the manifest points at:

```bash
dotnet publish Jellyfin.Plugin.Reelbase/Jellyfin.Plugin.Reelbase.csproj -c Release -o build/publish
cp build/publish/Jellyfin.Plugin.Reelbase.dll dist/
cd dist && zip -X reelbase_1.0.0.0.zip Jellyfin.Plugin.Reelbase.dll meta.json
md5sum reelbase_1.0.0.0.zip   # goes into manifest.json as "checksum"
```

Tests cover the API contract (against responses captured from a live instance), the search term
building, the ranking, the url handling and the credit mapping.

There is also an end-to-end probe that drives the real providers against a real instance:

```bash
REELBASE_API_KEY=rb_... dotnet run --project tools/ReelbaseProbe -c Release
```

## Licence

GPLv3. See [LICENSE](LICENSE). This plugin is derived from the Jellyfin IMVDb plugin, which is
GPLv3 as well.
