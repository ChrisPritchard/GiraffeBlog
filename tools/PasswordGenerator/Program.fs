open System
open System.Text
open System.Security.Cryptography

[<EntryPoint>]
let main _ =
    printfn "enter password to hash: "
    let password = Console.ReadLine ()

    printfn ""
    let salt = Guid.NewGuid().ToString()
    use hasher = SHA384.Create()
    let hashSource = salt + password |> Encoding.UTF8.GetBytes
    let newHash = hasher.ComputeHash hashSource |> Convert.ToBase64String
    
    printfn "update authors set Password = '%s,%s' where Username = '" newHash salt

0