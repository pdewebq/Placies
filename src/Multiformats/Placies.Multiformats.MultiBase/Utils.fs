namespace Placies.Multiformats

open System
open System.Buffers
open System.Runtime.CompilerServices
open Placies.Utils

[<AutoOpen>]
module ConvertExtensions =
    type Convert with
        static member FromBase64Chars(chars: ReadOnlySpan<char>): byte array =
            Convert.FromBase64String(chars.ToString())

        static member ToBase64StringNoPad(bytes: ReadOnlySpan<byte>): string =
            Convert.ToBase64String(bytes).TrimEnd('=')
        static member FromBase64StringNoPad(chars: ReadOnlySpan<char>): byte array =
            let paddingLength =
                match chars.Length % 4 with
                | 0 -> 0
                | 2 -> 2
                | 3 -> 1
                | _ -> invalidArg (nameof chars) "Invalid padding"
            let paddedChars =
                if paddingLength = 0 then
                    chars
                else
                    let paddedChars = (Array.zeroCreate<char> (chars.Length + paddingLength)).AsMemory()
                    chars.CopyTo(paddedChars.Span)
                    paddedChars.Span.Slice(chars.Length).Fill('=')
                    paddedChars.Span.AsReadOnly()
            Convert.FromBase64Chars(paddedChars)

        static member ToBase64StringUrl(bytes: ReadOnlySpan<byte>): string =
            Convert.ToBase64StringNoPad(bytes).Replace('+', '-').Replace('/', '_')
        static member FromBase64StringUrl(chars: ReadOnlySpan<char>): byte array =
            let replacedChars = chars.ToArray().AsSpan()
            chars.CopyTo(replacedChars)
            replacedChars.Replace('-', '+')
            replacedChars.Replace('_', '/')
            Convert.FromBase64StringNoPad(replacedChars)

[<Extension>]
type ByteArrayExtensions =
    [<Extension>]
    static member ToHexString(this: byte array): string =
        Convert.ToHexString(this)
