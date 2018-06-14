# Giraffe Blog Template

This is the slightly trimmed down source code of my personal website, minus one or two features that are my site-specific (e.g. we write stories there, so there is some functionality for that).

The tech stack for this is:

- Giraffe (1.1.0)
- Dotnet Core (2.1.0)
- Entity Framework Core

Thats it. Oh, a little custom css to make it look partially presentable, and a javascript file for editor functionality (autosave, html editing etc). Clean, simple and straight forward. All view html is created using the GiraffeViewEngine in the Views.fs file, something which is neat but which made debugging a bit tricky and so I am not sure I would recommend it. Does keep the publish footprint tight however.

To run this you will need MS SQL Server, either on prem or the Azure version (I've used both during development, and SQL Azure is what is used in production). This DB needs to have tables matching the schema presented in Data.fs: three tables for posts, comments and authors (users). Speaking of the last, authors have a password field that should be the combination of a cryptographically strong salt and a hashed password with that salt, the hashing being SHA384. The verification code in Data.fs should should help in reverse engineering the creation - otherwise ping me if you need help / care. The code is simple enough.

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