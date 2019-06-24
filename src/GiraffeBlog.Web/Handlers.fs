module Handlers

open System
open System.Security.Claims
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open FSharp.Control.Tasks.ContextInsensitive 
open Giraffe
open Data
open System.Text.RegularExpressions

type HttpContext with
    member __.IsAuthor = __.User.Identity.IsAuthenticated

let accessDenied : HttpHandler = 
    setStatusCode 401 >=> text "Access Denied"
let pageNotFound : HttpHandler = 
    setStatusCode 404 >=> text "Page Not Found"
let badRequest : HttpHandler = 
    setStatusCode 400 >=> text "Bad Request"

let error (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

let latest page = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let data = ctx.GetService<BlogData> ()

            // NOTE: disable the following line if you dont need it. Works best for sqlite
            data.Database.EnsureCreated () |> ignore

            let skipCount = page * 5
            let posts = query {
                for post in data.FullPosts () do
                    sortByDescending post.Date
                    skip skipCount
                    take 5
                    select post
            }
            return! htmlView (Views.latest ctx.IsAuthor posts page) next ctx
        }

let single key commentError = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let data = ctx.GetService<BlogData> ()
            let post = query {
                for post in data.FullPosts () do
                    where (post.Key = key)
                    select post
                }
            return! 
                match Seq.tryHead post with
                | Some p -> 
                    let isAuthorsPost = ctx.IsAuthor && p.Author.Username = ctx.User.Identity.Name
                    let view = Views.single ctx.IsAuthor isAuthorsPost p commentError
                    htmlView view next ctx
                | None -> pageNotFound next ctx
        }

let login : HttpHandler = htmlView (Views.login false false)

let private monthNames = 
    [""; "january"; "february"; "march"; "april";
    "may"; "june"; "july"; "august"; "september";
    "october"; "november"; "december"]

let archives = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let data = ctx.GetService<BlogData> ()
            let allByDate = query {
                for post in data.FullPosts () do
                    sortBy post.Date
                    select (post.Date.Month, post.Date.Year)
            }   
            let years = 
                allByDate 
                |> Seq.groupBy (fun (_,year) -> year)
                |> Seq.map (fun (year,posts) -> 
                    year, posts 
                        |> Seq.groupBy (fun (month,_) -> month) 
                        |> Seq.map (fun (month,posts) -> monthNames.[month],Seq.length posts))
            return! htmlView (Views.archives ctx.IsAuthor years) next ctx
        }

let month (monthName, year) = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let monthNumber = List.tryFindIndex (fun o -> o = monthName) monthNames
            return!
                match monthNumber with
                | None -> pageNotFound next ctx
                | Some m ->
                    let data = ctx.GetService<BlogData> ()
                    let posts = query {
                        for post in data.FullPosts () do
                            sortBy post.Date
                            where (post.Date.Year = year && post.Date.Month = m)
                            select post
                    }
                    let monthUrl targetMonth targetYear = sprintf "/month/%s/%i" targetMonth targetYear
                    let prevMonth = if m = 1 then monthUrl "december" (year - 1) else monthUrl monthNames.[m - 1] year
                    let nextMonth = if m = 12 then monthUrl "january" (year + 1) else monthUrl monthNames.[m + 1] year
                    htmlView (Views.month ctx.IsAuthor posts prevMonth nextMonth) next ctx
        }

let private trimToSearchTerm (term:string) content =
    let stripped = Regex.Replace(content, "<[^>]*>", "")
    let index = stripped.ToLower().IndexOf(term.ToLower())
    match index with 
    | -1 -> ""
    | _ -> 
        let start,stop = max (index - 20) 0, min (index + term.Length + 20) stripped.Length
        let section = stripped.Substring(start, stop - start)
        "..." + section + "..."

let search = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            return! 
                match ctx.TryGetQueryStringValue "searchTerm" with
                | None -> htmlView (Views.search ctx.IsAuthor None) next ctx
                | Some term ->
                    let data = ctx.GetService<BlogData> ()
                    let posts = query {
                            for post in data.FullPosts () do
                                where (post.Title.Contains(term) || post.Content.Contains(term))
                                sortByDescending post.Date
                                take 50
                                select post
                        } 
                    let results = 
                        posts 
                            |> Seq.map (fun p -> { p with Content = trimToSearchTerm term p.Content })
                            |> Seq.toList
                    htmlView (results |> Some |> Views.search ctx.IsAuthor) next ctx
        }

let about : HttpHandler = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            return! htmlView (Views.about ctx.IsAuthor) next ctx
        }

[<CLIMutable>]
type LoginForm = {
    username: string
    password: string
}

let setUserAndRedirect (next : HttpFunc) (ctx : HttpContext) (author: Author) =
    task {
        let issuer = sprintf "%s://%s" ctx.Request.Scheme ctx.Request.Host.Value
        let claims =
            [
                Claim(ClaimTypes.Name, author.Username,  ClaimValueTypes.String, issuer)
            ]
        let authScheme = CookieAuthenticationDefaults.AuthenticationScheme
        let identity = ClaimsIdentity(claims, authScheme)
        let user = ClaimsPrincipal(identity)
        do! ctx.SignInAsync(authScheme, user)
        
        return! redirectTo false "/" next ctx
    }

let tryLogin = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let! form = ctx.TryBindFormAsync<LoginForm> ()
            let badLogin () = htmlView (Views.login ctx.IsAuthor true) next ctx
            return! 
                match form with
                | Error _ -> badLogin ()
                | Ok form -> 
                    let data = ctx.GetService<BlogData> ()
                    let authors = query {
                        for user in data.Authors do
                            where (user.Username = form.username)
                            select user
                    }
                    match Seq.tryHead authors with
                    | None -> badLogin ()
                    | Some a ->
                        if a.Validate form.password then
                            setUserAndRedirect next ctx a
                        else badLogin ()
        }

[<CLIMutable>]
type NewComment = {
    author: string
    content: string
}

let createComment key = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let! newComment = ctx.TryBindFormAsync<NewComment> ()
            return! 
                match newComment with
                | Error _ -> badRequest next ctx
                | Ok c ->
                    let data = ctx.GetService<BlogData> ()
                    let post = query {
                        for post in data.FullPosts () do
                            where (post.Key = key)
                            select post
                    }
                    match Seq.tryHead post with
                    | None -> redirectTo false "/" next ctx
                    | Some p ->
                        if p.Comments.Count >= 20 then
                            badRequest next ctx
                        else if c.author = "" || c.content = "" then
                            single key Views.RequiredCommentFields next ctx
                        else if ["http:";"https:";"www."] |> List.exists (fun tk -> c.content.Contains(tk)) then
                            single key Views.InvalidCommentContent next ctx
                        else
                            data.Comments.Add 
                                ({ 
                                    Author = c.author
                                    Date = DateTime.Now
                                    Content = c.content
                                    Post_Key = key 
                                    Post = Unchecked.defaultof<Post>
                                    Id = 0}) |> ignore
                            data.SaveChanges() |> ignore
                            redirectTo false (sprintf "/post/%s#comments" key) next ctx
        }

let savedContentKey = "savedContent"

let editor key = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            return! 
                match key with
                | None -> 
                    let saved = ctx.Session.GetString(savedContentKey)
                    let model : Views.PostViewModel = { 
                        title = ""
                        content = if saved = null then "" else saved }
                    let view = Views.editor model Views.AutoSaveEnabled Views.NoEditorErrors
                    htmlView view next ctx
                | Some k ->
                    let data = ctx.GetService<BlogData> ()
                    let post = query {
                        for post in data.FullPosts () do
                            where (post.Key = k && post.Author.Username = ctx.User.Identity.Name)
                            select post
                    }
                    match Seq.tryHead post with
                    | None -> redirectTo false "/" next ctx
                    | Some p ->
                        let model : Views.PostViewModel = { title = p.Title; content = p.Content }
                        let view = Views.editor model Views.AutoSaveDisabled Views.NoEditorErrors
                        htmlView view next ctx
        }

let getKey (title: string) = 
    let clean = title.ToLower().Replace (" ", "-")
    Regex.Replace (clean, "[^A-Za-z0-9 -]+", "")

let createPost = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let! newPost = ctx.TryBindFormAsync<Views.PostViewModel> ()
            return! 
                match newPost with
                | Error _ -> 
                    badRequest next ctx
                | Ok form when form.title = "" || form.content = "" ->
                    let view = Views.editor form Views.AutoSaveEnabled Views.RequiredEditorFields
                    htmlView view next ctx
                | Ok form ->
                    let data = ctx.GetService<BlogData> ()
                    let key = getKey form.title
                    let existing = query {
                        for post in data.Posts do
                            where (post.Key = key)
                            select post
                        }
                    match Seq.tryHead existing with
                    | Some _ -> 
                        let view = Views.editor form Views.AutoSaveEnabled Views.ExistingPostKey
                        htmlView view next ctx
                    | None ->
                        let postEntity = {
                            Author_Username = ctx.User.Identity.Name
                            Author = Unchecked.defaultof<Author>
                            Key = key
                            Title = form.title
                            Content = form.content
                            Date = DateTime.Now
                            Comments = new System.Collections.Generic.List<Comment>()
                        }
                        data.Posts.Add(postEntity) |> ignore
                        data.SaveChanges() |> ignore
                        ctx.Session.Remove(savedContentKey)
                        redirectTo false (sprintf "/post/%s" key) next ctx
        }

let editPost key = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let! newPost = ctx.TryBindFormAsync<Views.PostViewModel> ()
            return! 
                match newPost with
                | Error _ -> badRequest next ctx
                | Ok form when form.title = "" || form.content = "" ->
                    let view = Views.editor form Views.AutoSaveDisabled Views.RequiredEditorFields
                    htmlView view next ctx
                | Ok form ->
                    let data = ctx.GetService<BlogData> ()
                    let post = query {
                        for post in data.FullPosts () do
                            where (post.Key = key && post.Author.Username = ctx.User.Identity.Name)
                            select post
                        }
                    match Seq.tryHead post with
                    | None -> badRequest next ctx
                    | Some p -> 
                        let key = getKey form.title
                        let existing = query {
                            for post in data.Posts do
                                where (post.Key = key && post.Key <> p.Key)
                                select post
                            }
                        match Seq.tryHead existing with
                        | Some _ -> 
                            let view = Views.editor form Views.AutoSaveDisabled Views.ExistingPostKey
                            htmlView view next ctx
                        | None ->
                            let updated = 
                                { p with 
                                    Key = key
                                    Title = form.title
                                    Content = form.content }
                            data.Entry(p).CurrentValues.SetValues(updated) |> ignore
                            data.SaveChanges() |> ignore
                            ctx.Session.Remove(savedContentKey)
                            redirectTo false (sprintf "/post/%s" key) next ctx
        }

let saveWork =
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let! body = ctx.BindJsonAsync<string>()
            ctx.Request.HttpContext.Session.SetString(savedContentKey, body)
            return! Successful.OK "" next ctx
        }