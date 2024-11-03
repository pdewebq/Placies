module Placies.Ipld.Tests.DagJsonTests

open Xunit
open Xunit.Abstractions
open Placies.Utils
open Placies.Multiformats
open Placies.Ipld.DagJson

type DagJsonTests(output: ITestOutputHelper) =

    static member GetDagJsonFixtures(): TheoryData<CodecFixture> =
        TheoryData<_>() {
            yield! IpldFixtures.readCodecFixtures MultiCodecInfos.DagJson.Name
        }

    static member FixturesToSkip = readOnlyDict [
        "float--1e-323", "Too large number"
        "float--8.940696716308594e-8", "Too large number"
        "float-1e-323", "Too large number"
        "float-8.940696716308594e-8", "Too large number"
        "int--11959030306112471732", "Too large number"
        "int-11959030306112471731", "Too large number"
        "int-18446744073709551615", "Too large number"
        "string-𐅑", "Supplementary pair escaping issue"
    ]

    [<SkippableTheory>]
    [<MemberData("GetDagJsonFixtures")>]
    member _.``Test fixtures reencoding``(fixture: CodecFixture): unit =
        let dagJsonCodec = DagJsonCodec(MultiBaseRegistry.CreateDefault())
        IpldFixtures.testReencoding output DagJsonTests.FixturesToSkip dagJsonCodec fixture
