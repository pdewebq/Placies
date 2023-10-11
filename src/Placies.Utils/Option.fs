namespace Placies.Utils

[<RequireQualifiedAccess>]
module Option =

    let ofTryByref (isSuccess: bool, value: 'a) : 'a option =
        match isSuccess, value with
        | true, value -> Some value
        | false, _ -> None
