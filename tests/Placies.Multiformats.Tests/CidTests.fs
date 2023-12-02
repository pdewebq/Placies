module Placies.Multiformats.Tests.CidTests

open Xunit
open Placies
open Placies.Multiformats

let multiBaseProvider: IMultiBaseProvider = MultiBaseRegistry.CreateDefault()
let multiCodecProvider: IMultiCodecProvider = MultiCodecRegistry.CreateDefault()
let multiHashProvider: IMultiHashProvider = MultiHashRegistry.CreateDefault()

[<Theory>]
[<InlineData("bafkreibm6jg3ux5qumhcn2b3flc3tyu6dmlb4xa7u5bf44yegnrjhc4yeq", "base32 - cidv1 - raw - (sha2-256 : 256 : 2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824)")>]
let ``Cid.toHumanReadableString`` (actualEncodedCid: string) (expectedHumanReadableCid: string) : unit =
    let cid = actualEncodedCid |> Cid.parse multiBaseProvider
    let multiBaseInfo = multiBaseProvider.TryGetByPrefix(actualEncodedCid.[0]) |> Option.get
    let actualHumanReadableCid = cid |> Cid.toHumanReadableString multiBaseInfo multiCodecProvider multiHashProvider
    Assert.Equal(expectedHumanReadableCid, actualHumanReadableCid)
