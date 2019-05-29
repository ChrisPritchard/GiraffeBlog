module Views

open Giraffe.GiraffeViewEngine

let layout isAuthor content =
    let head = 
        head [] [
            title [] [ rawText "Giraffe Blog Template" ]
            meta [ _name "description"
                   _content "A fully functional blog template in Giraffe, EF Core and ASPNET Core" ]
            link [ _rel "shortcut icon" 
                   _type "image/x-icon"
                   _href "/favicon.png" ]
            link [ _rel "stylesheet"
                   _type "text/css"
                   _href "/site.css" ]
        ]
    let sitehead = header [] [ h1 [] [ a [ _href "/" ] [ rawText "Giraffe Blog Template" ] ] ]
    let navigation = 
        nav [] [
            ul [] [
                li [] [ a 
                    [ _href (if isAuthor then "/new" else "/login") ] 
                    [ rawText (if isAuthor then "New Post" else "Login") ] ]
                li [] [ a [ _href "/archives" ] [ rawText "Archives" ] ]
                li [] [ a [ _href "/search" ] [ rawText "Search" ] ]
                li [] [ a [ _href "/about" ] [ rawText "About" ] ]
            ]
        ]
    html [] [
        head
        body [] [
            sitehead
            navigation
            section [ _class "content" ] content
            footer [] [ rawText "Giraffe Blog Template. Designed and coded by Christopher Pritchard, 2018" ]
        ]
    ]

let private listPost (post : Data.Post) = 
    article [] [
        h2 [] [ a [ sprintf "/post/%s" post.Key |> _href ] [ encodedText post.Title ] ]
        h5 [] [ 
                sprintf "posted by %s on %O" post.Author.DisplayName post.Date |> rawText
                a [ sprintf "/post/%s#comments" post.Key |> _href ] [ Seq.length post.Comments |> sprintf "comments (%i)" |> rawText ]
            ]
        section [] [ rawText post.Content ]
    ]

let latest isAuthor posts page = 
    let postList = posts |> Seq.toList |> List.map listPost
    let navLink pg txt = a [ sprintf "/page/%i" pg |> _href ] [ rawText txt ]
    let content = 
        postList @ 
        match page with 
        | 0 -> [ navLink 1 "Next Page" ] 
        | _ -> [ navLink (page - 1) "Previous Page";navLink (page + 1) "Next Page" ]
    layout isAuthor content

let private comment (c : Data.Comment) = [
        b [] [ sprintf "%s. %O" c.Author c.Date |> rawText ]
        p [] [ encodedText c.Content ] 
    ]

type CommentsError = | NoCommentError | RequiredCommentFields | InvalidCommentContent

let single isAuthor isOwnedPost (post : Data.Post) commentError = 
    let content = 
        [ 
            article [] [
                header [] [ h1 [] [ encodedText post.Title ] ]
                footer [] [ sprintf "%s. %O" post.Author.DisplayName post.Date |> rawText ]
                section [] [ rawText post.Content ]
            ] 
        ]
        @
        if isOwnedPost then [ a [ sprintf "/edit/%s" post.Key |> _href ] [ rawText "Edit" ] ] else []
        @
        [ 
            div [] [
                a [ _name "comments" ] []
                h2 [] [ rawText "Comments" ]
                post.Comments |> Seq.collect comment |> Seq.toList |> div []
            ] 
        ]
    let commentForm = [
        form [ _method "POST" ] [
            fieldset [] [
                label [ _for "author" ] [ rawText "Author" ]
                input [ _type "text"; _id "author"; _name "author" ]

                label [ _for "content" ] [ rawText "Content" ]
                textarea [ _rows "3"; _cols "50"; _id "content"; _name "content" ] []

                input [ _type "submit"; _value "Comment" ]

                (match commentError with
                | NoCommentError -> []
                | RequiredCommentFields -> [ rawText "Both author and content are required fields" ]
                | InvalidCommentContent -> [ rawText "Comments cannot contain links" ]) 
                    |> span [ _class "error-message" ]
            ]
        ]
    ]
    if post.Comments.Count >= 20 then 
        layout isAuthor content
    else
        layout isAuthor (content @ commentForm)

let login isAuthor wasError = 
    layout isAuthor [
        form [ _method "POST" ] [
            fieldset [] [
                label [ _for "username" ] [ rawText "Username" ]
                input [ _type "text"; _id "username"; _name "username" ]

                label [ _for "password" ] [ rawText "Password" ]
                input [ _type "password"; _id "password"; _name "password" ]

                input [ _type "submit"; _value "Login" ]
                span [ _class "error-message" ] [ rawText (if wasError then "Username and/or Password not recognised" else "") ]
            ]
        ]
    ]

let archives isAuthor (years : seq<int * seq<string * int>>) = 
    let yearList = 
        years |> Seq.map (fun (y,months) -> 
            div [ _class "archive-month" ] [ 
                h3 [] [ string y |> rawText ]
                months |> Seq.map (fun (m,c) -> 
                    li [] [ 
                        a [ sprintf "/month/%s/%i" m y |> _href ] [ sprintf "%s (%i)" m c |> rawText ] 
                    ]) |> Seq.toList |> ul []
             ]) |> Seq.toList
    let content = [
            [ h2 [] [ rawText "Archives" ]]
            yearList
            [ div [ _class "after-archive-months" ] [] ]
        ]
    List.concat content |> layout isAuthor

let month isAuthor posts prevUrl nextUrl = 
    let postList = posts |> Seq.toList |> List.map listPost
    let navLink url txt = a [ _href url ] [ rawText txt ]
    let content = 
        postList
        @ 
        [ navLink prevUrl "Previous Month"; navLink nextUrl "Next Month" ]
    layout isAuthor content

let search isAuthor (results: Data.Post list option) =
    let searchBox = [
            h2 [] [ rawText "Search" ]
            form [ _method "GET" ] [
                fieldset [] [
                    label [ _for "searchTerm" ] [ rawText "Search term" ]
                    input [ _type "text"; _id "searchTerm"; _name "searchTerm" ]
                    input [ _type "submit"; _value "Search"; _class "pure-button pure-button-primary" ]
                ]
            ]
            p [ ] [ rawText "Max 50 results. Note, searches can take some time." ]
        ]
    match results with
    | None -> layout isAuthor searchBox
    | Some r -> 
        let results = [
            h3 [] [ rawText "Results" ]
            r |> List.map (fun post -> 
                li [] [
                    a [ _href <| sprintf "/post/%s" post.Key ] [ h4 [] [ rawText post.Title ] ]
                    p [] [ rawText post.Content ]
                    span [] [ sprintf "Posted by %s on %O" post.Author.DisplayName post.Date |> rawText ]
                ]) |> ul []
        ]
        layout isAuthor (searchBox @ results)


[<CLIMutable>]
type PostViewModel = {
    title: string
    content: string
}

type EditorErrors = 
    | NoEditorErrors | RequiredEditorFields | ExistingPostKey

type EditorAutoSave = 
    | AutoSaveEnabled | AutoSaveDisabled

let editor (post : PostViewModel) autosave errors = 
    layout true [
        form [ _method "POST" ] [
            fieldset [] [
                label [ _for "title" ] [ rawText "Title" ]
                input [ _type "text"; _id "title"; _name "title"; _value post.title ]
                (match errors with
                | NoEditorErrors -> []
                | RequiredEditorFields -> [ rawText "Both title and content are required fields" ]
                | ExistingPostKey -> [ rawText "A post with a similar title already exists" ]) 
                    |> span [ _class "error-message" ]

                label [ _for "editor" ] [ rawText "Content" ]
                input [ _type "hidden"; _name "content" ]
                div [ _class "editor"; _contenteditable "true"; _id "editor" ] [ rawText post.content ]

                div [ _class "inline" ] [
                    label [] [
                        input [ _name "editmode"; _type "radio"; _value "rendered"; _checked ]
                        rawText "Rendered"
                    ]
                    label [] [
                        input [ _name "editmode"; _type "radio"; _value "html" ]
                        rawText "HTML"
                    ]
                ]
                
                input [ _type "submit"; _value "Submit"; _id "submit" ]
                (match autosave with | AutoSaveEnabled -> span [ _id "saving-status" ] [] | _ -> br [])

                script [ _type "text/javascript"; _src "/editor.js" ] []
            ]
        ]
    ]

let about isAuthor = 
    let content = [ 
        article [] [
            h2 [] [ rawText "TBC" ]
            p [] [
                rawText 
                    "Add about text in chunks like this."
            ]
        ] 
    ]
    layout isAuthor content
