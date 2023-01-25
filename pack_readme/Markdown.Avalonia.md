Markdown.Avalonia render Markdown with avalonia ui. It transform markdown to controls (ex: Border, Grid, TextBlock). 

This package depends on AvaloniaEdit for syntax highlighting.
If you don't want it, use [Markdown.Avalonia.Tight](https://www.nuget.org/packages/Markdown.Avalonia.Tight) instead.

**How to use**  

Markdown.Avalonia supports three methods to render markdown.

1. [Load .md from AvaloniaResource](https://github.com/whistyun/Markdown.Avalonia/wiki/How-to-use#Load-.md-from-AvaloniaResource)
   ```xml
   <md:MarkdownScrollViewer
      Source="avares://YourAssemblyName/Assets/Test.md"/>
   ```
2. [Write markdown in xaml](https://github.com/whistyun/Markdown.Avalonia/wiki/How-to-use#Write-markdown-in-xaml)
   ```xml
   <md:MarkdownScrollViewer>
     # Heading1
     Hello Markdown.Avalonia!
   </md:MarkdownScrollViewer>
   ```
3. [With binding](https://github.com/whistyun/Markdown.Avalonia/wiki/How-to-use#With-binding)
   ```xml
   <md:MarkdownScrollViewer
      Markdown="{Binding MdText}"/>
   ```

**Preview**  
![sc1](https://raw.githubusercontent.com/whistyun/Markdown.Avalonia/master/docs/img.demo/scrn1.png)  
![sc2](https://raw.githubusercontent.com/whistyun/Markdown.Avalonia/master/docs/img.demo/scrn2.png)  