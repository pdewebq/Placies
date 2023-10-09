namespace Placies.Ipld.Tests

open System.IO
open Placies
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
