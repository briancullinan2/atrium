# Atrium

A spaced repetition studying app. Prepopulated with study course content. Build on M$ MAUI Blazor because C# is nice.

## Build

#### Android

dotnet publish Atrium/Atrium.csproj -f net10.0-android -c Release -r android-arm64 --self-contained

dotnet build RazorSharp/RazorSharp.csproj -f net10.0-android -c Debug -r android-arm64 --self-contained

dotnet build $project -f net10.0 -c Debug

dotnet build Atrium/Atrium.csproj -f net10.0-browser -c Debug -r browser-wasm

dotnet build Retheme/Retheme.csproj -f net10.0 -c Debug

dotnet build Hosting/Hosting.csproj -f net10.0-browser -c Debug

dotnet build Clippy/Clippy.csproj -f net10.0 -c Debug -r android-arm64 --self-contained

dotnet build RazorSharp/RazorSharp.csproj -f net10.0-windows10.0.19041.0 -c Debug -r win-x64 --self-contained

dotnet build DataStore/DataStore.csproj -f net10.0 -c Debug -r ios-arm64 --self-contained

dotnet build $project -f net10.0-maccatalyst -c Debug -r maccatalyst-x64 --self-contained

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

#### 4/19/2026

I think I need to expand on blazor as a service, I realized on mobile I can generate an serve our own pages with an http responder, but I'm also realizing
blazor supports this template system I wrote in PHP years ago where it's sort half callback like drupal, and half API like vue.js and blazor. 
I'm doing some small context with menus -> plugin, but i'll need a main layout insert for the theme detection. home and auth inserts, context menus need
work as they aren't showing up. but i used this system to build a console menu, "drawbox" i called it, it would draw a text frame any size in the console 
but it would center your input text inside it, so i used it to make "sections" for all my forms which were also well defined like drupal forms. I
sort of hated that specificity, I like the reflection metadata thing present in this better than anything I've written in PHP. This is model -> html
like ruby on rails.


I spoke to soon. Fuck microsoft. Their fucking bullshit lifecycle has NEVER worked.

#### 4/16/2026

This is a fairly solid foundation. I reorganized and rebuilt and my framework is still working. I even tried to ruin up Microsoft's framework lifecycle
and abuse it into becoming unstable like my fathers did to me. Nothing. It still works, unfortunately, I can't even use the burden of my
own poor design as an excuse not to keep working on it. I want to get full database synchronization working with user and permissions again.
This time it is pluggable, so the hosting service still has to save and render settings and enabled state despite not having a database
to save it in. In EPIC I used rewritable configuration files before the database initialized. This allowed administrators to put it in
"admin do anything mode" for medical staff to meet the requirement, you're not allowed to have a device in the field that can't be accessed because 
of some administrative policy. Medical devices must be usable in their environment, why doesn't this principal apply to my car? Or even
my own PC? Or my Playstation. All of these contexts make me unhappy, even a device meant to bring joy, is particularly designed to spite me.

What works: new statically callable menu system supported by this InvokeService() method that filters the parameter list through the dependency
injector just like HttpContext .Map(route, method) functions do for us on a normal web server context. I didn't remember this feature from Mvc.
So that was a nice surprise, I did similar stuff to express in node. I think this is how its supposed to work, I was going to add even more
injectable situations so you could eventually just through methods at it and it will derive the context. This works better than any loosely
typed node handler I've written for RPC. It might even be worth making that wrapper/pulling in the wrapper from my notebook and adding all
my cells as a service to C#, just thinking out loud for fun, maybe too exotic.

Finally think I have the renderstate responding to a valid full page load. Not a missed page or accidentally getting into the service container
which the whole SignalR circuit thing is wacky btw. I'll explain. In the demo project you have a desktop project, a web server project, and a web assembly client project.
If you add a service according to the demo and documentation, you have to update all 3 of these projects and add code that fits all 3 of these
platforms separately, possibly even different compile targets. I'm killing this in the seed. I hate frameworks that make me repeat myself.
This is all part of my evil plan to turn every client into an injectable service container. I even imagined rewriting Microsoft blazor Hub as a mock
just so I could do all this in a background service worker. I'm sure Google drive does more evil things to service workers than I ever could.

More whacky shit. Page titles. Microsoft ships with some internal Page title control, so you write the title inside that at least once. Then right
below it or the layout somehow, you want to show the page title on the actual page, so you write it again. then you want to make your main layout
show a menu of all your pages so your write the page title again in the menu. then comes the tricky part, you want to make a user management system
and change the page title to reflect the name of the user currently being editing. good luck, lol.

Weirdest whacky shit. URIs. Have any of us ever remembered a day that Microsoft didn't have broken links? Maybe they should consider a different 
framework. I haven't found a pattern where the page render happens and I can't find a URI or component but by my own inefficient design. Scanning
all the assemblies for routable types and then backtracking the string link onto the RouteAttribute (@path) until a best match fitting the parameters/formatting
is found. I actually can't put this one on Microsoft, this is industry wide standard. If I want a link on a page or to a page I write the URL
to that page a dozen times wherever it's contextually relevant. Industry standard is, if the page URL then changes, you have to update all
the URLs on every page, and the run your automated testing system to make sure it still works. At least now with GetUri&lt;TComponent&gt;() 
the test will crash on page generation instead of running through extra steps.

Finally, the dream. A framework that runs on every platform that I can define with formal methods and as long as I stay within the framework
and don't have to do anything too complicated I'll NEVER have to write a formal method again because it will already be well defined.
So here it is. The best I could do at having an opinion. I'd give blazor 9/10. The highest rating I've ever given a framework. I'd say it's
even cooler than Observables. Better than react, vue, angular. Thank you Microsoft for building something we can all enjoy for free. 
Spectactular work.

TODO: need to test design of plugin page on mobile/non-windows platforms, web version needs work too.

#### 4/15/2026

TODO: figure out how to use the query manager to INotifyPropertyChange across databases, was talking to Gemini about using the -wal sqlite file
inotify from the FS watcher to run a check on a readonly nonblocking sqlite connection, so the server readonly the desktop and desktop readonly
the server and they both look at each others "change" table thats keyed (tableName, primaryKey) that stores the previous and new values
of every change. I built this in clearview for change tracking also.


#### 4/12/2026

Commit messages are getting long. I broke everything up into separate projects so help with build time and separation of concerns. 
Projects only have 3 or 4 dependencies. Mostly everything refers to Interfacing which just contains all the service interfaces and hardly 
anything else. I want to get to the point where I can click enable and disable on each module, Users, flash cards, course, hosting, 
themer, everything and it basically live updates and turns on and off. Then i want to build out hosting as a whole process server service 
that can launch games servers as windows startup services and also has web assembly join now demo mode on the home screen and discord  
intergration from C# side would interesting, this i thought it would cool to tack on my acquisition stuff from my old media server since 
it's going to be so modular. I might as well as my website activity tracker and rebuild my personal home page while i'm at it.
Anyways, it's getting big and out of scope.


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

* DONE: EntityMetadata, this Object.Metadata(), and MetadataControl patterns working well. Priority #1: write as little &lt;html&gt; control code as possible, model and css only
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

