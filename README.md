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

#### 4/21/2026

I hate that I reach a point where either the Title breaks, the most basic feature of a page, or I have to repeat myself a dozen times
around my site retyping all the relevant links into every page to give it context. Even stranger is setting page titles
with a special blazor element that can update the title with javascript, but there's no other way for me to outlet that control
other than to override it and stick my title in to a service that my layout can later estimate when it should read because there's
no way to subscribe to a components internal life-cycle except to create injectable properties that are usually required even if 
I try to make it optional/nullable. at this point i'm making my own lifecycle inside a component that may or may not be in memory.
Based on context, the lifecycle could have disposed on my title control after I rendered the html page but inside of the blazor circuit
so I have a good state the exists when the page is rendered and that gets completely discarded for a brand new service container when the
app suddenly connects through proprietary web sockets protocols that no one asked for. then it resets my page state for the rest of
the session it made itself out of sync because somewhere in my code i tried to rely on navigationmanager and that didn't work
then i tried to rely on ihttpcontextaccessor and that doesn't work because "_blazor/" connections don't have a route to the original
page that was rendered. there's no reference to it and it isn't making use of referrer because of cors policy (also a who technology
nobody asked for). then i tried to rely directly on router which should be the source of reason to get a page intention from,
and that turns out to be the absolute worst of all. 


no matter what order i put my assemblies in, i can't control which page it 
selects internally or the order of routes, i can't put a bunch of different catch-alls at the bottom like we used to iis httpcontext
and i can't control it with attributes like I did with MVC. then when the route is finally rendered, it doesn't happen in the
OnNavigation callback, there's no Type available, instead i have to render the control synchronously to get then intended control
type out that it plans on rendering. so the only thing the router apparently does FOR me is the part that i already worked out
perfectly on my own which is SPA navigation.history management. so i'd complain, but this is perfectly definitive of my reality
all the parts that i toiled to work out on my own, if i was some sort of megalomaniac, it's like somebody stole and white-labeled
and their profiting off of selling my success as their own brand. but obviously i can't take credit for microsofts work, i'm just
pointing out the bizarre relationship between the parts microsoft profits from are the parts I don't need from them. like the 
old adage says, forgive them, for they know not what they do. they've figured out a very specific method of torment. by showing 
me how they corrupt other developers into believing in their designs. every programmer at microsoft should be ashamed for never 
solving a single problem or inventing plenty more of their own.

so i'll be rewriting my own navigation manager that supplies the correct address and iserviceprovider context to every client
whether it comes from httpcontext, maui desktop, or some circuit bullshit i'll also have to rewrite. maybe i'll write a 
Microsoft.AspNetCore.SignalR.Protocols.Quake3 module that uses Q3 diffs to support multi-player "browser-as-a-renderer" bullshit.

i can't believe navigationmanager was my breaking point, it seemed so obvious at first and then my page stopped redirecting properly
and that should have been my first clue, this should have been when i stopped using navigationmanager/httpcontext/uncontrollable 
service containers/and was also the breaking point for component as a service. NavigateTo&lt;TComponent&gt;(this NavigationManager Nav
I should have never ever needed to write my own reverse router. i should have stopped using navigationmanager and router the very
absolute first moment i realized "where is my control type?" and then had to struggle through APIs to figure out what control
it's trying to render. now, because i load my main extension assembly on top and plugins as "AdditionalAssemblies" my route 
resolution is completely uncontrollable and not even defined by the router, there's zero way for me to see any decision it makes
except dig into microsoft source code, which completely defeats the purpose of "writing an API" that they haven't figured out
after 30 years. so i can't control what page blazor picks easily, and i can't see what pages it might pick, and i can't control
the order of the pages it might pick. 

i've about had it up to here with the ijsruntime. in all my years of programming on quake 3 web assembly, i ain't never let 
somebody or something tell me when i'm allowed to run javascript. poor programmers. i hope microsoft never shows this shit 
to anybody. it's an embarrassment. classically, there's only one pattern that makes this difficult that microsoft is too
stupid to solve without me. i solved it by my own design in 2008 with very little real-framework experience, i had merely used 
spring formally, and abe's game engine. 


> i realized quickly with php, there's a problem of accumulating my page content,
> checking then killing session fopen to help multi-threading, then sending http-headers based on page content, then writing html head
> to client, then writing css/javascript features, finally writing the relevant page content, then booting javascript to act like
> an SPA after the page loads, all of this has to happen in the correct order without interference just to prevent page flash.

i've spent the last 20 years of my life devoted to this exact render pipe-line. only microsoft should tell me it's completely out of
my control and i don't deserve to work there. fuck microsoft. after 20 years they come back and they've reintroduce interaction
bugs, the page freezes while web assembly boots in their demo. and the rest of the pipeline instead of trying to fix the problems
that plague it by working together, they've built this entire framework around covering up their errors and bad social skills.

so lets write it out. i can't bitch without offering solutions. this is how i solved these rendering problems in my past framework.
this is what i'll make blazor do for me just because I like runtime generics. 

1. the problem of accumulating page content - in php i used virtual output buffers a lot to keep my html inside of functions cleaner
because i was already adding an extra layer of function wrappers around every output method. effectively turning every piece
of html in a static renderer for that particular piece of data. i built a form wrapper similar to drupal's/even modelled after drupals.
i think i copied their supported control types at one point from the docs. i've don't the same BS here but i feel kind of horrid about
it:

```csharp
public static async Task<string> ToHtml(this RenderFragment? fragment, IServiceProvider? serviceProvider)
    {
        if (fragment == null) return string.Empty;
        serviceProvider ??= new ServiceCollection().AddLogging().BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        using var renderer = new HtmlRenderer(serviceProvider, loggerFactory);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            // Use the private wrapper defined below
            var output = await renderer.RenderComponentAsync<FragmentWrapper>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                { nameof(FragmentWrapper.Content), fragment }
                })
            );

            return output.ToHtmlString();
        });
    }
```

it should have never come to this. look what you made me do microsoft. i hate that __builder is synchronous and render to html is not.
so the only thing i'm going to gain is the .razor file format because microsoft ruined everything else. nobody has ever liked asp
encrypted page state, i wish they'd stop basing their entire life-cycle on it. it doesn't solve sink pollution. 

this whole entire concept of singleton and scoped, i love in principal, organizationally, i've arranged a lot of code very quickly
it's all decoupled by interfaces and the best part, DI solved that problem where you want to include one control from one file
and another control from another file, but the second control also refers to the first control. there's a Lazy<> for more obvious
or simply calling GetService<>() from inside the control instead of the constructor. perfect. but this whole concept of like
a singleton is the lifetime of the app and scoped is just for a single page render, but then in maui desktop scoped is the life
time of the page unless the developer hits refresh. in httpcontext scoped is the lifetime of the page render, except after
blazor circuit is started and then it's the lifetime of the circuit. and then in webassembly it's pretty much all singleton 
because the javascript context owns the whole memory space. all these contexts could be cool if they actually worked.
the fact that blazor circuit starts a new service container for a client is awesome, except that it doesn't match the service 
container it started for the prerender, so now everything is out of sync. unless, i need a singleton for the whole server instance
which if that crashes, also crashes my server. 

i almost forgot to mention how much i hate this component life-cycle. so imma be real concrete with this one. if i write an html component
and put it in markup &lt;component&gt; and my component in a module attached to a custom html element:
https://caniuse.com/?search=Custom+Elements i control that element, not blazor. i tell it when to render, not the framework. so the whole
design principal of "only update the elements needed" is completely ruining by confining a person to "OnInitialize", "OnParametersSet", and
"OnAfterRenderAsync" which all appear to be completely meaningless when you try to collide 3 different contexts into a single app.
this solved nothing with rendering, it solved nothing with state or context or being cross platform and microsoft could have made a
framework without this non-sense and it could have been awesome. i just realized there's type recognition for javascript, if language
server is already doing it why can't i write javascript directly in C#, why even compile to web assembly, just use babel-msil.
they could convert javascript to C# or C# to javascript so you don't need IJSRuntime or IJSObjectReference at all ever, 
it just runs in whatever context it's meant for. microsoft could accomplish this probably by rewriting just the module container
for chrome and nobody would ever have to write for the web in javascript or web assembly again, you could run msil directly in 
the browser scope without npapi and any other "native-speed" vm could register inside the sandbox.

so microsoft has screwed over 3 top-down pyramids that they tried to inverse the control of for "convenience" or some other bigger
plan probably to use AI to get the internet to kill itself or something. the router they inverted, the router tries to now tell
me what page it found but only from inside that component after its load can i know for sure the right page was hit. 
they tried to take control away from rendering by turning it into
a scoped state machine, but all my components are inconsistently attached to scoped or singleton services, it doesn't make any
sense how i can supply a IServiceProvider to the component system, and then that service provider uses some internal service provider
to build the rest of the scope that's entirely out of my control. and why the fuck are we using dependency injection when by design
it doesn't support plugins, which means it doesn't support project decoupling, which means by design microsofts idea of dependency
injection is actually an anti-pattern that prevents you from injecting the dependencies. and only writing it my way reversed that
control flow.

2. checking then killing session fopen to help multi-threading - i did this php, it wasn't a great flow, basically pull the session
 information out at the very beginning before headers are sent and then close the file so if another file request starts its not blocked
 by the first page in a multi-threaded server environment. i had to do this in iis httpcontext, maybe this is an auth thing. maybe
 it doesn't inherently create file locks for how i'm planning on handling identities. i created identity as a convenience, and humans 
 started to identify with their identities and that's like having false idols. my playstation controller is based on false-idols 
 because all it cares about is who is logged in. wtf? 

3. then sending http-headers based on page content - drupal solved this, i solved this by my own estimations even lacking experience
 stop forcing somebody to use your page title control and simply ask the life-cycle, "what page are you?". then everybody knows no 
 matter what context or time it is. no race conditions. i have the same problem with the default "not-found" controls. why do i 
 have to write my own control to properly set a 404 + content on an SPA. did i pick the wrong project type for blazor? i thought
 i checked all the boxes possible but it left out offline mode too. which i had to rewrite from microsofts template because it
 doesn't support it's own version management, which means microsofts default offline.js SPA template that hopefully nobody is
 using but me is out on the web inside service workers permanently. stuck carelessly inside people's cache stored forever. does
 anybody wonder where cancer comes from? its your browser cache. come at me FDA

4. then writing html head to client, then writing css/javascript features - i tried the built in accumulator microsoft calls Sections.
 from the very beginning it stopped updating at the right time. i realized because when the section content changes i also have
 to call StateHasChanged() on the container, and at that point i should just use a service and not use a section.

5. then writing css/javascript features - somehow i managed to add page flash back into my app. at first, i was excited about scoped
 css. then i wanted to write a study-mode where one control changed the page layout through a `bool` in a service. immediately started
 hating css `::deep` designation which doesn't work on root elements. I will not be using any feature that is not built into css.
 thank god they added variables to make theming easier. &lt;style scoped&gt; microsoft doesn't have my respect on this topic until
 they make this standard work. stop ruining the web.

6. finally writing the relevant page content - now this comes with relief and contempt. my friend (he's dead now) wanted me to help
 him write some advertising platform before AI started speeding up. in the advertising realm, all these stupid little "accept cookies" 
 dialogs comes from GDPR regulators writing the instruction manual to torture me. because they are toxic assholes and don't listen
 to reason, all the advertisers added stupid little popups to ruin the web more. the alternative to showing you a targeted ad
 is targeting the ad towards the page content. they could choose to be good and advertise based on stuff relevant to what you like
 but instead the entire advertising has turned evil and now they only market based on who they want you to be. thousands of humans, 
 career politicians, have based their careers on destroying the web as a good experience. what's truly incredible is that anybody
 should expect me to be nice about my unraveling.

 in my own designs, i had a system wide file indexer in php that could dive into zip files and serve just their streamed innards over
 the web. i've replicated this for pk3s and quake3 in javascript. i don't remember if i did this C# yet. but there should be 
 no lifecycle based reason why i can't easily set a 404 or a response type, and my IIS configuration was mostly exactly the 
 same as what i remember from MVC. i think i even copied some of my cors code from the other project. blazor makes this completely
 not obvious. i used to just pass the httpcontext to the MVC function and it could write it's own response and close if it chose to.
 by default in microsofts own demo project there is a separate server project that dual boots the blazor circuit and any api
 callbacks your app needs through .Map() functions, i think the weather demo app is supposed to demonstrate this with a service.
 so the pipe-line has somehow changed into, if the user clicks "download" on a file on my website, blazor circuit can now transport
 those bytes through a websocket and I can make a file uri download in javascript after encoding once through the circuit and 
 encoding again through javascript file uri. or i can use navigationmanager and use javascript to redirect them to a file that
 goes back through the server, and either has to go directly to a route or back through the blazor router to find a service?
 none of this is obvious now because of microsoft. don't even get my started on file open dialogs, it crashed the app pretty quickly
 which makes me wonder, does electron struggle with opening files?

7. then booting javascript to act like and SPA after the page loads - i was great at this before web assembly came along. i'm not sure
 downloading 50 MB of framework files into the browser is going to work for me. it was okay for quake3 but my boot i intentionally tried
 to keep under 10 MB. now i have to figure out how to force microsoft dependencies to be even lazier than as lazy as i already know
 how to make them just to get the app to start up lighter. this has already been extremely challenging, and the built in auto-loader
 is broken. modern frameworks keep trying to "take over the page". react, angular, blazor, they all do this and it's ridiculous.
 none of these big inflatable companies are solving any problems for me as a programmer. they are simply writing their APIs and 
 instructions for how to torment me for eternity. 
 
 > the core problem, i'll restate here in case anybody is confused. how do i render my template when my server state changes?

 microsoft, google, react, thousands of humans devoting their careers to building worthless pyramids of computer source code all because
 they have a very basic misunderstanding of that sentence. my very next question is this, "can i render my templates in C#?" and the 
 answer is turning out to be a resounding NO. I am more likely at this point to integrate webkit into quake 3 and render my templates
 in openGL than I am to be able to create a working application with web assembly C#. my dream was killed after continuing to get
 context errors after debugging my web assembly build again to meet up with maui desktop. and the error of context/circuit versus prerender
 what is stored in memory what is scoped, all entirely OUT of my control when i hit the "Create project" button. microsoft drove me
 to this level of insanity and it shows in my code what i was trying to overcome at the time i built it. it's no secret anymore.
 i can't rely on the service container supplied by microsoft frameworks. they are inconsistently rendered. i made my own composite
 service container that i will run as a singleton in every memory space and that will control the scope based context. whatever
 circuit or this "browser as a renderer" was supposed to be, they completely missed the mark. maybe i can use rendertreebuilder __builder 
 i'm not even excited, i'm going to have to abandon .razor files in favor of HTML and string.Replace() if i ever want to use C# in a 
 web browser. i'm completely blown away with how disappointing this entire stack is. it started off great, and every bug i tried to fix
 was ruined by the frameworks nuance. "oh why can't I use this function in my click handler?", oh why isn't my property binding? because i
 put the `@` sign in the wrong place? why didn't the IDE warn me, it has bullshit for everything else? why is my app failing to start?
 because nuget automatically installed the wrong package version because microsoft locked all the packages behind the 
 &lt;FrameworkReference&gt; property in your project file, completely anti-pattern defeating the entire purpose of nuget was so that
 microsoft would STOP doing that with packages. that's the whole reason nuget was invented and microsoft just self-defeated because
 of `.csproj` files. after learning Make, that shit makes me more miserable than CMake, congratulations microsoft, making devs 
 miserable one schizo at a time.

8. all of this has to happen in the correct order without interference just to prevent page flash - i just realized the reason i started 
 writing all this. preventing interface flashing, because i tried to use the microsoft CSS outlet control instead of listing all my
 global css files at the top. i can't use blazor scoped css because some controls affect each other. i can't use `::deep` because
 it doesn't work from the root, only down from the container. i can list all my individual css files at the top like its 2005 in head.
 but then as a programmer i have to properly scope everything, i can't just copy and paste from a GPT. I managed to recreate browser
 flash in blazor simply by trying to make a css outlet that supports plugins and basic scoping. even the most basic needs i struggle
 to write within this framework. I don't remember MVC ever being this nuanced. i had server C# and javascript control code and they
 were separate and the page always felt SPA-ish. putting C# into the browser or putting HTML into a server renderer shouldn't change
 anything about the development experience except make platforming glorious. that's not what happened here. this descent is satan making
 fun of me. he's laughing at me like "haha, see how many billions of lines of code are at your fingertips for free and you can't make a 
 single line meaningful without me".


 It's a turning point when i stop posting screen shots of progress and instead post IDE errors. microsoft technology has and always
 will be hell for developers. stay the fuck away.

 ![VS bugs](./Docs/Screenshot%202026-04-21%20121131.png?raw=true)




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

