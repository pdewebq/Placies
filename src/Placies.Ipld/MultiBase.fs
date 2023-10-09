namespace Placies.Multiformats

type MultiBaseInfo = {
    Name: string
    PrefixCharacter: char
}

[<RequireQualifiedAccess>]
module MultiBaseInfos =

    let Base32 = { Name = "base32"; PrefixCharacter = 'b' }
