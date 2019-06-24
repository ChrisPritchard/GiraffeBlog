# Giraffe Blog Template

This is the slightly trimmed down source code of my personal website, minus one or two features that are my site-specific (e.g. we write stories there, so there is some functionality for that).

The tech stack for this is:

- Giraffe (3.6.0)
- Dotnet Core (2.2)
- Entity Framework Core

Thats it. Oh, a little custom css to make it look partially presentable, and a javascript file for editor functionality (autosave, html editing etc). Clean, simple and straight forward. All view html is created using the GiraffeViewEngine in the Views.fs file, something which is neat but which made debugging a bit tricky and so I am not sure I would recommend it. Does keep the publish footprint tight however.

The system is configured to use Sqlite as a backend store, but will work fine with any other database technology supported by EF Core (originally I was using SQL Server in Azure, for example, with almost no code changes between now and then). This Sqlite db will be created on first launch by [this line](https://github.com/ChrisPritchard/GiraffeBlog/blob/master/src/GiraffeBlog.Web/Handlers.fs#L34). To set passwords for users, you can use the password generator console app under tools. The app generates a portion of an update script for updating a given user account using the [Sqlite3 CLI tool](https://sqlite.org/cli.html) - that tool is also quite useful for creating other data. Please ping me if you have any problems with this, cheers.

## Features

Functionality this blog template has:

- latest five posts, with prev/next page links
- single post with comments, and comment creator box
	- posts are referenced by key, a cleaned version of their title (e.g. /post/post-name)
	- a max of 20 comments is enforced
	- comments cannot accept links (this is to prevent automated spammers)
- an archives page, showing posts grouped by year and month (the blog this is for has 12 years of posts, averaging 20 a month)
- a search post, which bluntly trawles post content for a search term
- an about page
- login page for authors
- the ability to edit or create a new post
	- autosave every ten seconds on new posts, just storing content into session
	- editor is a div with contenteditable enabled, and a js switch to toggle being rendered and html value

## Disclaimer

This template is for those who are interested, particularly those who want to create a site using the excellent Giraffe, EF Core and ASPNET Core. The code was developed for my personal use on my blog, and as a bit of a learning exercise. Accordingly:

- It is probably not production quality - you might not want to take this, slap a theme on it, and shove it in front of a million users
- I started learning F# two months ago. While this knowledge has been built on a foundation of 15 years in C#, I am still rough and probably do some things in here suboptimally

That being said, its Unilicense. Do as you please!