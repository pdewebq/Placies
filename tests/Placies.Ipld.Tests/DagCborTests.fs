module Placies.Ipld.Tests.DagCborTests

open Xunit
open Xunit.Abstractions
open Placies.Utils
open Placies.Multiformats
open Placies.Ipld.DagCbor

type DagCborTests(output: ITestOutputHelper) =

    static member GetDagCborFixtures(): TheoryData<CodecFixture> =
        TheoryData<_>() {
            yield! IpldFixtures.readCodecFixtures MultiCodecInfos.DagCbor.Name
        }

    static member FixturesToSkip = readOnlyDict [
        "int--11959030306112471732", "Too large number"
        "int-11959030306112471731", "Too large number"
        "int-18446744073709551615", "Too large number"
    ]

    [<SkippableTheory>]
    [<MemberData("GetDagCborFixtures")>]
    member _.``Test fixtures reencoding``(fixture: CodecFixture): unit =
        let dagCborCodec = DagCborCodec()
        IpldFixtures.testReencoding output DagCborTests.FixturesToSkip dagCborCodec fixture
