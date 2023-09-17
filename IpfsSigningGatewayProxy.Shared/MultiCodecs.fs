namespace IpfsSigningGatewayProxy

open System
open System.Numerics
open System.Text
open Ipfs.Registry

module MultiCodec =

    let registerMore () =

        Codec.Register("cidv1", 0x1) |> ignore
        Codec.Register("sha2-256", 0x12) |> ignore
        Codec.Register("varsig", 0x34) |> ignore
        Codec.Register("dns", 0x35) |> ignore
        Codec.Register("libp2p-key", 0x72) |> ignore
        Codec.Register("ipfs", 0xe3) |> ignore
        Codec.Register("ipns", 0xe5) |> ignore
        Codec.Register("rsa-pub", 0x1205) |> ignore

module MultiBase =

    let encodeAnyBase (baseAlphabet: string) (input: byte array) : string =
        let mutable number = BigInteger(input)
        let l = baseAlphabet.Length
        let result = StringBuilder()
        while number > BigInteger.Zero do
            let idx = number % BigInteger(l) |> int
            if idx >= l then failwith ""
            result.Append(baseAlphabet.[idx]) |> ignore
            number <- number / BigInteger(l)
        result.ToString()

    let decodeAnyBase (baseAlphabet: string) (input: string) : byte array =
        let mutable result = BigInteger(0)
        let b = baseAlphabet.Length
        let mutable pow = 0
        for c in input.ToCharArray() |> Array.rev |> String do
            let idx = baseAlphabet.IndexOf(c)
            if idx = -1 then failwith ""
            result <- result + BigInteger.Pow(b, pow) * BigInteger(idx)
            pow <- pow + 1
        result.ToByteArray(isUnsigned=true, isBigEndian=true)

    let base36Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz"

    let registerMore () =
        MultiBaseAlgorithm.Register(
            "base36", 'k',
            encodeAnyBase base36Alphabet,
            decodeAnyBase base36Alphabet
        ) |> ignore
