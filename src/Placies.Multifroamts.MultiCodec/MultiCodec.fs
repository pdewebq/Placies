namespace Placies.Multiformats


type MultiCodecInfo = {
    Name: string
    Code: int
}

type IMultiCodecProvider =
    abstract TryGetByCode: code: int -> MultiCodecInfo option
    abstract TryGetByName: name: string -> MultiCodecInfo option
