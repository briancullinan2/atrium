# Atrium

A spaced repetition studying app. Prepopulated with study course content. Build on M$ MAUI Blazor because C# is nice.

## Build

#### Android

dotnet publish Atrium/Atrium.csproj -f net10.0-android -c Release -r android-arm64 --self-contained


## History

I worked on this app years ago and got paid a lot of money to do it. I got hung up on the data management stuff, I build this JavaScript -> PHP template engine thing that made
the whole system tightly coupled and hard to modify. Any change to the template that the data or table names didn't match up would crash the whole page.
Oddly, my skills on building the data marshaller didn't translate to better foundational design, like crashing the JavaScript page because I lacked type safety, silly reasons.
Handling lots of data is and always will be a nightmare for me, so I built the form generator using reflection so I have 1 less mode of maintenance. Controls, html + css layout on pages + 
JS validation, and data model can finally be reduced to controls, css, data model (including validation attributes).
I tried to write some stuff in Vue.JS and I really liked the appearance of the controls and my CSS rendering, but I was kind of depressed from the server/client split architecture.
I remember writing a pretty nice plain DOM JavaScript uploader with Node.JS backend for Atrium 4 but that's about where it ended.
I spent so much time building these panels to control the permission model, I got lost on it and wondered if I should have just written a "select from Google drive" option or a 
upload Anki format option. So in this version I'll add all of it.
The only reason I am here is because I heard about 2 years ago while I was working on game stuff that M$ ported .Net Core to web assembly. I also heard Linq and runtime Generics were 
available in the browser, something TypeScript couldn't even accomplish.
I added CSS scoping and PHP -> JavaScript before php-babel was a meme.

#### 4/4/2026

1. Publish my own site, edit-anywhere revival? smaller life-tracker combo?
2. Game server with background demo web assembly and discord integration
3. Media server revival like ampache, had an idea to connect devices and controls for dad
4. Home security, personal status tracker, maybe some people over work and they need reminders not to check email?
5. Medical device and cloud data combo app like EpicCentral and ClearView combined



#### 4/3/2026

Doing another big layout refresh to make sure that I don't cross domains/scopes in purpose. Makes the code more reusable for other projects I can use the same
basic framekwork and layout and swap out entire menus and pages with the changed of an environment start-up variable and treat all my purposes like plugins/build targets.



#### 3/18/2026
TODO: whats the holdup, merge any missing default users with database results, but only for admin view
   so many splits in reality, i can save settings, get an oauth flow working, store the setting for
   default guest user, start loading settings/config with "auto login" on desktop, store theme settings
   for user accounts, and finally and most importantly, build the firewall for query manager to only show 
   the default users to admin, this involved the ILoginService and IAuthService to work together

All related to just getting this line working properly in my head:

CurrentUsers = DataLayer.Generators.Users.Generate().ToList();

Never ask me why I smoke. I'm not saying I'm special or alone in this experience, I'm just saying I'm not enjoying it. This is the only skill I have and it isn't worth anything.


#### 3/16/2026

I've designed this storage mechanism and testing suite I want to write down before I forget, and then I can compare if it came out
true to the original ideas. In Study Sauce I made a validation page that showed th result of all the "integration tests".
I didn't write full unit tests, but I wrote integration tests that tested the overall functionality of every page through selenium.
Here I'd combine my on going experience and integrate the code coverage from node istanbul, don't know why it's called that.
And I'd write attributes to control the integration test entirely instead of listing conditions in seperate test files.
The suite would be a status report builder basically building the result at runtime.

For storage, in the previous version we had an iOS app that would start synching content as soon as you log in. Instead of
risking locking up the UX, I'm synching between disk and memory in a separate thread to support UX functions, then synching between 
clients and UX and backend and storage. All these different contexts basically extend the same DataLayer.TranslationContext
The point is to be descriptive at the moment the interface is interacting with the data. For example, if I'm working on Anki imported 
data, I can show the UI and start saving rows to disk in the background in two separate actions. But in the web client, I'd have
to wait on the server response to save all the data I just extracted and then transition the UX to the card editor. Or I'd have
to move all the potentially temporary data from the importer into the permanent storage and update the UI from remote. In this 
scenario, I'm giving up on clien C# processing power almost entirely. 

But with my design to synchronize data and change the context based on the service, I don't actually really have to make those decisions.
I can assume data integrity by negotiating a few pathways to the data, and then my client/page views don't have to make all the synching
decisions. Right now I have EphemeralStorage and PersistentStorage, i'll probably add remote, testing ephemeral and testing persistent.
I'm going to add a priority queue where UX lists can request data ahead of say background synching content downloads.
Another example this is useful, in the iOS app the synch would start right away, on web the synch had to happen up front on page request.
With Blazor that sort of solves both the UX threading and the piece-wise data, and matching that same functionality on the client.

The process I will show the pack list of Due cards based on Response table, doesn't require full card structure. Then background synch all
the users packs from disk into the memory, or remote into memory for web clients. On web clients, subsequent queries will act on loaded 
data instead of referring to SQL configuration.





#### 3/15/2026

I am adding this theme editor for the app and I thought it looked cool enough to share.

![Settings](./Docs/Screenshot%202026-03-15%20003359.png?raw=true)

![Home](./Docs/Screenshot%202026-03-15%20003808.png?raw=true)

![Packs](./Docs/Screenshot%202026-03-15%20003926.png?raw=true)

![Study](./Docs/Screenshot%202026-03-15%20004257.png?raw=true)



#### 3/9/2026

Added basic landing pages for most functionality planned. Needs lots of merchantising. Needs more solid login and sessions and connected accounts. Getting spaced repetition and card
editor working first. Just tried it on Android build and it works! But needs lots of formatting and fixing, scolling issues on menu.

#### 2/22/2026

Added a strictly typed NavigateTo(), GetUri() system because broken links suck! Using strong typing on as much dynamic layout content as possible so if something moves or names change
the compiler will stop it and not have to wait for testing suite.

## TODO

* DONE: EntityMetadata, this Object.Metadata(), and MetadataControl patterns working well. Priority #1: write as little fucking &lt;html&gt; control code as possible, model and css only
* DONE: Anki, Google, legacy format importer/uploader
* Distributed cloud encrypted backups, strong local storage, guest experience, row level data marshalling with IQuerable instead of Postgres
* Subscription and single sale through Venmo, Google, Apple Pay, Square, multiple authorizer API support
* Pre-rendered DRM streaming support, controlled content leaves memory and renders live as an image instead of copy/paste content
* DONE: Needs videos to be remade from script and AI? Entire course content include in basic local access, quizes, study plan creator, pack builder utility
* Content management and sales panel that shows how similar other content is to yours for possible copyright but really just for technical capabilities
* Add background and title bar and styling options to packs like we had planned in the last version

## More TODO
Erasure Coding Math	Library	Witteborn.ReedSolomon (NuGet). It's a port of the Backblaze Java lib. Do not write the Galois Field math yourself; it's a rabbit hole of performance traps.
Secret Sharing (SSS)	Library	SecretSharingDotNet. SSS is just polynomial interpolation. Use a library to handle the finite field math so you don't leak bits through integer rounding.
Network Transport	Write/Wrap	libp2p. There is a libp2p-dotnet, but if it’s too raw, many devs use a Sidecar (a small Go/Rust binary) that the C# app talks to via gRPC/Localhost for the actual P2P heavy lifting.
"Buddy" Protocol	Write	This is your secret sauce. The logic that says "Node A is a buddy of Node B" and manages the heartbeats/shuffles of shards.
Permission Chains	Library	UCANs. Use a UCAN library (or the JWT specs) to handle the "who can see what."

