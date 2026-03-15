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

* Priority #1: write as little fucking &lt;html&gt; control code as possible, model and css only
* Anki, Google, legacy format importer/uploader
* Distributed cloud encrypted backups, strong local storage, guest experience, row level data marshalling with IQuerable instead of Postgres
* Subscription and single sale through Venmo, Google, Apple Pay, Square, multiple authorizer API support
* Pre-rendered DRM streaming support, controlled content leaves memory and renders live as an image instead of copy/paste content
* Entire course content include in basic local access, quizes, study plan creator, pack builder utility
* Content management and sales panel that shows how similar other content is to yours for possible copyright but really just for technical capabilities
* Add background and title bar and styling options to packs like we had planned in the last version

## More TODO
Erasure Coding Math	Library	Witteborn.ReedSolomon (NuGet). It's a port of the Backblaze Java lib. Do not write the Galois Field math yourself; it's a rabbit hole of performance traps.
Secret Sharing (SSS)	Library	SecretSharingDotNet. SSS is just polynomial interpolation. Use a library to handle the finite field math so you don't leak bits through integer rounding.
Network Transport	Write/Wrap	libp2p. There is a libp2p-dotnet, but if it’s too raw, many devs use a Sidecar (a small Go/Rust binary) that the C# app talks to via gRPC/Localhost for the actual P2P heavy lifting.
"Buddy" Protocol	Write	This is your secret sauce. The logic that says "Node A is a buddy of Node B" and manages the heartbeats/shuffles of shards.
Permission Chains	Library	UCANs. Use a UCAN library (or the JWT specs) to handle the "who can see what."

