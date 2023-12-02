namespace Placies.Multiformats

open System
open System.Runtime.CompilerServices

[<AutoOpen>]
module ConvertExtensions =
    type Convert with
        static member ToBase64StringNoPad(bytes: byte array): string =
            Convert.ToBase64String(bytes).TrimEnd('=')
        static member FromBase64StringNoPad(s: string): byte array =
            let s =
                match s.Length % 4 with
                | 0 -> s
                | 2 -> s + "=="
                | 3 -> s + "="
                | _ -> invalidArg (nameof s) "Invalid padding"
            Convert.FromBase64String(s)

        static member ToBase64StringUrl(bytes: byte array): string =
            Convert.ToBase64StringNoPad(bytes).Replace('+', '-').Replace('/', '_')
        static member FromBase64StringUrl(s: string): byte array =
            Convert.FromBase64StringNoPad(s.Replace('-', '+').Replace('_', '/'))

[<Extension>]
type ByteArrayExtensions =
    [<Extension>]
    static member ToHexString(this: byte array): string =
        Convert.ToHexString(this)
