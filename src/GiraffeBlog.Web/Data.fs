module Data

open System
open System.Text
open System.ComponentModel.DataAnnotations
open System.ComponentModel.DataAnnotations.Schema
open System.Security.Cryptography
open Microsoft.EntityFrameworkCore

[<CLIMutable>]
type Post = {
    [<Key>]
    Key: string
    Title: string
    Author_Username: string
    [<ForeignKey("Author_Username")>]
    Author: Author
    Date: DateTime
    Content: string
    Comments: System.Collections.Generic.List<Comment>
} and [<CLIMutable>]Author = {
    [<Key>]
    Username: string
    Password: string
    DisplayName: string
    ImageUrl: string
} and [<CLIMutable>]Comment = {
    Id: int
    Post_Key: string
    [<ForeignKey("Post_Key")>]
    Post: Post
    Author: string
    Date: DateTime
    Content: string
}

type BlogData (options) = 
    inherit DbContext (options)

    [<DefaultValue>] val mutable authors : DbSet<Author>
    member __.Authors with get() = __.authors and set v = __.authors <- v
    [<DefaultValue>] val mutable posts : DbSet<Post>
    member __.Posts with get() = __.posts and set v = __.posts <- v
    [<DefaultValue>] val mutable comments : DbSet<Comment>
    member __.Comments with get() = __.comments and set v = __.comments <- v

    member __.FullPosts () = __.Posts.Include(fun p -> p.Author).Include(fun p -> p.Comments)

type Author with
    member __.Validate passwordToCheck =
        match __.Password.Split(',') with
        | [|hash;salt|] ->
            let newHashSource = salt + passwordToCheck |> Encoding.UTF8.GetBytes
            use hasher = SHA384.Create()
            let newHash = hasher.ComputeHash newHashSource |> Convert.ToBase64String
            newHash = hash
        | _ -> false