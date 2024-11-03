namespace Placies.Ipld.Tests

open System.Collections.Generic
open System.IO
open System.Text
open Xunit
open Xunit.Abstractions
open Swensen.Unquote
open Placies
open Placies.Utils
open Placies.Utils.Collections
open Placies.Ipld
open Placies.Multiformats

type FixtureEntry = {
    CodecName: string
    Cid: Cid
    DataBytes: byte array
}

type Fixture = {
    Name: string
    Entries: FixtureEntry list
}

type CodecFixture = {
    Name: string
    Cid: Cid
    DataBytes: byte array
}

[<RequireQualifiedAccess>]
module IpldFixtures =

    let read () = seq {
        let multibaseProvider = MultiBaseRegistry.CreateDefault()
        for fixtureDir in Directory.EnumerateDirectories("./codec-fixtures/fixtures") do
            let fixtureName = Path.GetFileName(fixtureDir)
            let entries = [
                for fixtureFile in Directory.EnumerateFiles(fixtureDir) do
                    let fileName = Path.GetFileName(fixtureFile)
                    let cidStr, codecName = fileName.Split('.') |> Array.exactlyTwo
                    let data = File.ReadAllBytes(fixtureFile)
                    yield {
                        CodecName = codecName
                        Cid = Cid.parse multibaseProvider cidStr
                        DataBytes = data
                    }
            ]
            { Name = fixtureName
              Entries = entries }
    }

    let readCodecFixtures (codecName: string) = seq {
        for fixture in read () do
            for fixtureEntry in fixture.Entries do
                if fixtureEntry.CodecName = codecName then
                    yield {
                        Name = fixture.Name
                        Cid = fixtureEntry.Cid
                        DataBytes = fixtureEntry.DataBytes
                    }
    }

    let testReencoding (output: ITestOutputHelper) (fixturesToSkip: IReadOnlyDictionary<string, string>) (codec: ICodec) (fixture: CodecFixture) : unit =
        output.WriteLine($"Fixture '%s{fixture.Name}'")
        ( let condition, reason = fixturesToSkip.TryGetValue(fixture.Name)
          Skip.If(condition, reason) )

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
        let dataModelNode = codec.TryDecodeAsync(dataStream) |> Task.runSynchronously |> ResultExn.getOk

        output.WriteLine("Decoded DataModel node:")
        output.WriteLine($"%A{dataModelNode}")
        output.WriteLine("")

        use reencodedDataStream = new MemoryStream()
        let reencodedCid = codec.TryEncodeWithCidAsync(reencodedDataStream, dataModelNode, 1, MultiHashInfos.Sha2_256) |> Task.runSynchronously |> ResultExn.getOk
        let reencodedDataBytes = reencodedDataStream.ToArray()

        output.WriteLine("Reencoded bytes:")
        output.WriteLine(reencodedDataBytes.ToHexString())
        output.WriteLine("")
        output.WriteLine("Reencoded text:")
        output.WriteLine(Encoding.UTF8.GetString(reencodedDataBytes))
        output.WriteLine("")
        output.WriteLine($"Reencoded CID: {reencodedCid}")

        test <@ Cid.encode fixture.Cid = Cid.encode reencodedCid @>
