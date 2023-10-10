module Placies.Ipld.Tests.DagJsonTests

open System.IO
open System.Text
open Xunit
open Xunit.Abstractions
open Swensen.Unquote

open Placies
open Placies.Multiformats
open Placies.Ipld
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
        output.WriteLine($"Fixture '%s{fixture.Name}'")
        ( let condition, reason = DagJsonTests.FixturesToSkip.TryGetValue(fixture.Name)
          Skip.If(condition, reason) )

        let dagJsonCodec = DagJsonCodec(MultiBaseRegistry.CreateDefault())

        output.WriteLine("Fixture bytes:")
        output.WriteLine(fixture.DataBytes.ToHexString())
        output.WriteLine("")
        output.WriteLine("Fixture text:")
        output.WriteLine(Encoding.UTF8.GetString(fixture.DataBytes))
        output.WriteLine("")
        output.WriteLine($"Fixture CID: {fixture.Cid}")
        output.WriteLine("")
        output.WriteLine("")

        use dataStream = new MemoryStream(fixture.DataBytes)
        let dataModelNode = (dagJsonCodec :> ICodec).Decode(dataStream) |> Result.getOk

        output.WriteLine("Decoded DataModel node:")
        output.WriteLine($"%A{dataModelNode}")
        output.WriteLine("")

        use reencodedDataStream = new MemoryStream()
        let reencodedCid = Codec.encodeWithCid dagJsonCodec 1 MultiHashInfos.Sha2_256 dataModelNode reencodedDataStream |> ResultExn.getOk
        let reencodedDataBytes = reencodedDataStream.ToArray()

        output.WriteLine("Reencoded bytes:")
        output.WriteLine(reencodedDataBytes.ToHexString())
        output.WriteLine("")
        output.WriteLine("Reencoded text:")
        output.WriteLine(Encoding.UTF8.GetString(reencodedDataBytes))
        output.WriteLine("")
        output.WriteLine($"Reencoded CID: {reencodedCid}")

        test <@ Cid.encode fixture.Cid = Cid.encode reencodedCid @>
