namespace Placies.Ipld.Tests

open System.IO
open Placies

type FixtureEntry = {
    CodecName: string
    Cid: Cid
    DataBytes: byte array
}

type Fixture = {
    Name: string
    Entries: FixtureEntry list
}

module IpldFixtures =

    let read () = seq {
        for fixtureDir in Directory.EnumerateDirectories("./codec-fixtures/fixtures") do
            let fixtureName = Path.GetFileName(fixtureDir)
            let entries = [
                for fixtureFile in Directory.EnumerateFiles(fixtureDir) do
                    let fileName = Path.GetFileName(fixtureFile)
                    let cidStr, codecName = fileName.Split('.') |> Array.exactlyTwo
                    let data = File.ReadAllBytes(fixtureFile)
                    yield {
                        CodecName = codecName
                        Cid = Cid.parse cidStr
                        DataBytes = data
                    }
            ]
            { Name = fixtureName
              Entries = entries }
    }