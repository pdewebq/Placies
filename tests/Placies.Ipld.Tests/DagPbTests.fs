module Placies.Ipld.Tests.DagPbTests

open System
open System.Buffers
open System.IO
open System.Text
open Xunit
open Xunit.Abstractions
open Swensen.Unquote
open Placies
open Placies.Utils
open Placies.Multiformats
open Placies.Ipld
open Placies.Ipld.DagPb

type DagPbTests(output: ITestOutputHelper) =

    static member GetDagPbFixtures(): TheoryData<CodecFixture> =
        TheoryData<_>() {
            yield! IpldFixtures.readCodecFixtures MultiCodecInfos.DagPb.Name
        }

    static member FixturesToSkip = readOnlyDict [ ]

    [<Fact>]
    member _.``Basic encode test``() : unit =
        let bufferWriter = ArrayBufferWriter()
        let expectedBytes = Convert.FromHexString("12340a2212208ab7a6c5e74737878ac73863cb76739d15d4666de44e5756bf55a2f9e9ab5f431209736f6d65206c696e6b1880c2d72f12370a2212208ab7a6c5e74737878ac73863cb76739d15d4666de44e5756bf55a2f9e9ab5f44120f736f6d65206f74686572206c696e6b18080a09736f6d652064617461")
        let pbNode = {
            Data = ValueSome (ReadOnlyMemory(Encoding.UTF8.GetBytes("some data")))
            Links = [|
                {
                    Hash = Cid.tryParseV0 "QmXg9Pp2ytZ14xgmQjYEiHjVjMFXzCVVEcRTWJBmLgR39U" |> Result.getOk
                    Name = ValueSome "some link"
                    Tsize = ValueSome 100000000uL
                }
                {
                    Hash = Cid.tryParseV0 "QmXg9Pp2ytZ14xgmQjYEiHjVjMFXzCVVEcRTWJBmLgR39V" |> Result.getOk
                    Name = ValueSome "some other link"
                    Tsize = ValueSome 8uL
                }
            |]
        }
        DagPbEncode.writeNode pbNode bufferWriter
        let actualBytes = bufferWriter.WrittenSpan.ToArray()
        output.WriteLine("Actual bytes:")
        output.WriteLine(Convert.ToHexString(actualBytes))
        Assert.Equal(expectedBytes, seq actualBytes)

    [<SkippableTheory>]
    [<MemberData("GetDagPbFixtures")>]
    member _.``Test fixtures reencoding``(fixture: CodecFixture): unit =
        output.WriteLine($"Fixture '%s{fixture.Name}'")
        ( let condition, reason = DagPbTests.FixturesToSkip.TryGetValue(fixture.Name)
          Skip.If(condition, reason) )

        let dagPbCodec = DagPbCodec()

        output.WriteLine("Fixture bytes:")
        output.WriteLine(fixture.DataBytes.ToHexString())
        output.WriteLine("")
        output.WriteLine($"Fixture CID: {fixture.Cid}")
        output.WriteLine("")
        output.WriteLine("")

        use dataStream = new MemoryStream(fixture.DataBytes)
        let dataModelNode = (dagPbCodec :> ICodec).TryDecodeAsync(dataStream) |> Task.runSynchronously |> ResultExn.getOk

        output.WriteLine("Decoded DataModel node:")
        output.WriteLine($"%A{dataModelNode}")
        output.WriteLine("")

        use reencodedDataStream = new MemoryStream()
        let reencodedCid = (dagPbCodec :> ICodec).TryEncodeWithCidAsync(reencodedDataStream, dataModelNode, 1, MultiHashInfos.Sha2_256) |> Task.runSynchronously |> ResultExn.getOk
        let reencodedDataBytes = reencodedDataStream.ToArray()

        output.WriteLine("Reencoded bytes:")
        output.WriteLine(reencodedDataBytes.ToHexString())
        output.WriteLine("")
        output.WriteLine($"Reencoded CID: {reencodedCid}")

        test <@ Cid.encode fixture.Cid = Cid.encode reencodedCid @>
