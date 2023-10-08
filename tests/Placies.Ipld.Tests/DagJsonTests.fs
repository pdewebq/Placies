module Placies.Ipld.Tests.DagJsonTests

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open Ipfs
open Xunit
open Xunit.Abstractions
open Swensen.Unquote

open Placies
open Placies.Ipld
open Placies.Ipld.DagJson

// let fixtures () = seq {
//     "array-2",
//         "[2]",
//         DataModelNode.List [
//             DataModelNode.Integer 2
//         ]
//     "array-255",
//         "[255]",
//         DataModelNode.List [
//             DataModelNode.Integer 255
//         ]
//     "array-3,4,5,6",
//         "[3,4,5,6]",
//         DataModelNode.List [
//             DataModelNode.Integer 3
//             DataModelNode.Integer 4
//             DataModelNode.Integer 5
//             DataModelNode.Integer 6
//         ]
//     "array-5-nested",
//         """["array",["of",[5,["nested",["arrays","!"]]]]]""",
//         DataModelNode.List [
//             DataModelNode.String "array"
//             DataModelNode.List [
//                 DataModelNode.String "of"
//                 DataModelNode.List [
//                     DataModelNode.Integer 5
//                     DataModelNode.List [
//                         DataModelNode.String "nested"
//                         DataModelNode.List [
//                             DataModelNode.String "arrays"
//                             DataModelNode.String "!"
//                         ]
//                     ]
//                 ]
//             ]
//         ]
//     "array-500",
//         "[500]",
//         DataModelNode.List [
//             DataModelNode.Integer 500
//         ]
//     "array-6433713753386423",
//         "[6433713753386423]",
//         DataModelNode.List [
//             DataModelNode.Integer 6433713753386423L
//         ]
//     "array-65536",
//         "[65536]",
//         DataModelNode.List [
//             DataModelNode.Integer 65536
//         ]
//     "array-9007199254740991",
//         "[9007199254740991]",
//         DataModelNode.List [
//             DataModelNode.Integer 9007199254740991L
//         ]
//     "array-empty",
//         "[]",
//         DataModelNode.List [ ]
//     "array-mixed",
//         """[6433713753386423,65536,500,2,0,-1,-3,-256,-2784428724,-6433713753386424,{"/":{"bytes":"YTE"}},"Čaues ßvěte!"]""",
//         DataModelNode.List [
//             DataModelNode.Integer 6433713753386423L
//             DataModelNode.Integer 65536
//             DataModelNode.Integer 500
//             DataModelNode.Integer 2
//             DataModelNode.Integer 0
//             DataModelNode.Integer -1
//             DataModelNode.Integer -3
//             DataModelNode.Integer -256
//             DataModelNode.Integer -2784428724L
//             DataModelNode.Integer -6433713753386424L
//             DataModelNode.Bytes (Convert.FromBase64String("YTE"))
//             DataModelNode.String "Čaues ßvěte!"
//         ]
//     "bytes-a1",
//         """{"/":{"bytes":"oQ"}}""",
//         DataModelNode.Bytes (Convert.FromBase64StringNoPadding("oQ"))
//     "bytes-empty",
//         """{"/":{"bytes":""}}""",
//         DataModelNode.Bytes (Convert.FromBase64String(""))
//     "bytes-long-8bit",
//         """{"/":{"bytes":"AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUVJTVFVWV1hZWltcXV5fYGFiY2RlZmdoaWprbG1ub3BxcnN0dXZ3eHl6e3x9fn+AgYKDhIWGh4iJiouMjY6PkJGSk5SVlpeYmZqbnJ2en6ChoqOkpaanqKmqq6ytrq+wsbKztLW2t7i5uru8vb6/wMHCw8TFxsfIycrLzM3Oz9DR0tPU1dbX2Nna29zd3t/g4eLj5OXm5+jp6uvs7e7v8PHy8/T19vf4+fr7/P3+"}}""",
//         DataModelNode.Bytes (Convert.FromBase64String("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUVJTVFVWV1hZWltcXV5fYGFiY2RlZmdoaWprbG1ub3BxcnN0dXZ3eHl6e3x9fn+AgYKDhIWGh4iJiouMjY6PkJGSk5SVlpeYmZqbnJ2en6ChoqOkpaanqKmqq6ytrq+wsbKztLW2t7i5uru8vb6/wMHCw8TFxsfIycrLzM3Oz9DR0tPU1dbX2Nna29zd3t/g4eLj5OXm5+jp6uvs7e7v8PHy8/T19vf4+fr7/P3+"))
//     "cid-QmQg1v4o9xdT3Q14wh4S7dxZkDjyZ9ssFzFzyep1YrVJBY",
//         """{"/":"QmQg1v4o9xdT3Q14wh4S7dxZkDjyZ9ssFzFzyep1YrVJBY"}""",
//         DataModelNode.Link (Cid.decode "QmQg1v4o9xdT3Q14wh4S7dxZkDjyZ9ssFzFzyep1YrVJBY")
//     "cid-QmRgutAxd8t7oGkSm4wmeuByG6M51wcTso6cubDdQtuEfL",
//         """{"/":"QmRgutAxd8t7oGkSm4wmeuByG6M51wcTso6cubDdQtuEfL"}""",
//         DataModelNode.Link (Cid.decode "QmRgutAxd8t7oGkSm4wmeuByG6M51wcTso6cubDdQtuEfL")
//     "cid-QmXg9Pp2ytZ14xgmQjYEiHjVjMFXzCVVEcRTWJBmLgR39V",
//         """{"/":"QmXg9Pp2ytZ14xgmQjYEiHjVjMFXzCVVEcRTWJBmLgR39V"}""",
//         DataModelNode.Link (Cid.decode "QmXg9Pp2ytZ14xgmQjYEiHjVjMFXzCVVEcRTWJBmLgR39V")
//     "cid-arrayof",
//         """[{"/":"bafyreidykglsfhoixmivffc5uwhcgshx4j465xwqntbmu43nb2dzqwfvae"},{"/":"bafybeidskjjd4zmr7oh6ku6wp72vvbxyibcli2r6if3ocdcy7jjjusvl2u"},{"/":"baf4bcfgio3hovkftaer3yx6jsnm6navhg4yimwi"},{"/":"QmXg9Pp2ytZ14xgmQjYEiHjVjMFXzCVVEcRTWJBmLgR39V"},{"/":"QmQg1v4o9xdT3Q14wh4S7dxZkDjyZ9ssFzFzyep1YrVJBY"},{"/":"QmRgutAxd8t7oGkSm4wmeuByG6M51wcTso6cubDdQtuEfL"},{"/":"bafkreiebzrnroamgos2adnbpgw5apo3z4iishhbdx77gldnbk57d4zdio4"},{"/":"bagcqcera73rupyla6bauseyk75rslfys3st25spm75ykhvgusqvv2zfqtucq"},{"/":"bafkreifw7plhl6mofk6sfvhnfh64qmkq73oeqwl6sloru6rehaoujituke"},{"/":"bafyreidj5idub6mapiupjwjsyyxhyhedxycv4vihfsicm2vt46o7morwlm"},{"/":"bafyreiejkvsvdq4smz44yuwhfymcuvqzavveoj2at3utujwqlllspsqr6q"},{"/":"bahaacvrabdhd3fzrwaambazyivoiustl2bo2c3rgweo2ug4rogcoz2apaqaa"},{"/":"bahaacvrasyauh7rmlyrmyc7qzvktjv7x6q2h6ttvei6qon43tl3riaaaaaaa"},{"/":"bagyqcvraypzcitp3hsbtyyxhfyc3p7i3226lullm2rkzqsqqlhnxus7tqnea"},{"/":"bagyacvradn6dsgl6sw2jwoh7s3d37hq5wsu7g22wtdwnmaaaaaaaaaaaaaaa"},{"/":"bafkqabiaaebagba"}]""",
//         DataModelNode.List [
//             DataModelNode.Link (Cid.decode "bafyreidykglsfhoixmivffc5uwhcgshx4j465xwqntbmu43nb2dzqwfvae")
//             DataModelNode.Link (Cid.decode "bafybeidskjjd4zmr7oh6ku6wp72vvbxyibcli2r6if3ocdcy7jjjusvl2u")
//             DataModelNode.Link (Cid.decode "baf4bcfgio3hovkftaer3yx6jsnm6navhg4yimwi")
//             DataModelNode.Link (Cid.decode "QmXg9Pp2ytZ14xgmQjYEiHjVjMFXzCVVEcRTWJBmLgR39V")
//             DataModelNode.Link (Cid.decode "QmQg1v4o9xdT3Q14wh4S7dxZkDjyZ9ssFzFzyep1YrVJBY")
//             DataModelNode.Link (Cid.decode "QmRgutAxd8t7oGkSm4wmeuByG6M51wcTso6cubDdQtuEfL")
//             DataModelNode.Link (Cid.decode "bafkreiebzrnroamgos2adnbpgw5apo3z4iishhbdx77gldnbk57d4zdio4")
//             DataModelNode.Link (Cid.decode "bagcqcera73rupyla6bauseyk75rslfys3st25spm75ykhvgusqvv2zfqtucq")
//             DataModelNode.Link (Cid.decode "bafkreifw7plhl6mofk6sfvhnfh64qmkq73oeqwl6sloru6rehaoujituke")
//             DataModelNode.Link (Cid.decode "bafyreidj5idub6mapiupjwjsyyxhyhedxycv4vihfsicm2vt46o7morwlm")
//             DataModelNode.Link (Cid.decode "bafyreiejkvsvdq4smz44yuwhfymcuvqzavveoj2at3utujwqlllspsqr6q")
//             DataModelNode.Link (Cid.decode "bahaacvrabdhd3fzrwaambazyivoiustl2bo2c3rgweo2ug4rogcoz2apaqaa")
//             DataModelNode.Link (Cid.decode "bahaacvrasyauh7rmlyrmyc7qzvktjv7x6q2h6ttvei6qon43tl3riaaaaaaa")
//             DataModelNode.Link (Cid.decode "bagyqcvraypzcitp3hsbtyyxhfyc3p7i3226lullm2rkzqsqqlhnxus7tqnea")
//             DataModelNode.Link (Cid.decode "bagyacvradn6dsgl6sw2jwoh7s3d37hq5wsu7g22wtdwnmaaaaaaaaaaaaaaa")
//             DataModelNode.Link (Cid.decode "bafkqabiaaebagba")
//         ]
// }

type FixtureEntry = {
    CodecName: string
    Cid: Cid
    DataBytes: byte array
}

type Fixture = {
    Name: string
    Entries: FixtureEntry list
}

type DagJsonTests(output: ITestOutputHelper) =

    static do ShipyardMultiCodec.registerMore ()

    // static member GetFixtures(): obj array seq = seq {
    //     for name, jsonString, expectedDataModelNode in fixtures () do
    //         yield [| name; jsonString; expectedDataModelNode |]
    // }
    //
    // [<Theory>]
    // [<MemberData("GetFixtures")>]
    // member _.``Test fixtures decode``(_name: string, jsonString: string, expectedDataModelNode: DataModelNode): unit =
    //     let actualDataModelNodeRes = DagJson.tryDecode (JsonNode.Parse(jsonString))
    //     test <@ Ok expectedDataModelNode = actualDataModelNodeRes @>
    //
    // [<Theory>]
    // [<MemberData("GetFixtures")>]
    // member _.``Test fixtures redecoding``(_name: string, _jsonString: string, dataModelNode: DataModelNode): unit =
    //     let jsonNode = DagJson.encode dataModelNode
    //     let dataModelNodeRedecoded = DagJson.tryDecode jsonNode
    //     test <@ Ok dataModelNode = dataModelNodeRedecoded @>
    //
    // [<Theory>]
    // [<MemberData("GetFixtures")>]
    // member _.``Test fixtures reencoding``(_name: string, jsonString: string, _dataModelNode: DataModelNode): unit =
    //     let jsonNode = JsonNode.Parse(jsonString)
    //     let dataModelNode = DagJson.tryDecode jsonNode |> Result.getOk
    //     let jsonNodeReencoded = DagJson.encode dataModelNode
    //     let jsonSerializerOptions =
    //         let options = JsonSerializerOptions(JsonSerializerOptions.Default)
    //         options.WriteIndented <- false
    //         options
    //     let jsonStringReencoded = JsonSerializer.Serialize(jsonNodeReencoded, jsonSerializerOptions)
    //     test <@ jsonString = jsonStringReencoded @>

    // ----

    static member GetFixtures2(): obj array seq = seq {
        let fixtures = seq {
            for fixtureDir in Directory.EnumerateDirectories("./codec-fixtures/fixtures") do
                let fixtureName = Path.GetFileName(fixtureDir)
                let entries = [
                    for fixtureFile in Directory.EnumerateFiles(fixtureDir) do
                        let fileName = Path.GetFileName(fixtureFile)
                        let cidStr, codecName = fileName.Split('.') |> Array.exactlyTwo
                        let data = File.ReadAllBytes(fixtureFile)
                        yield {
                            CodecName = codecName
                            Cid = Cid.decode cidStr
                            DataBytes = data
                        }
                ]
                { Name = fixtureName
                  Entries = entries }
        }
        for fixture in fixtures do
            yield [| box fixture |]
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
    [<MemberData("GetFixtures2")>]
    member _.``Foo``(fixture: Fixture): unit =
        ( let condition, reason = DagJsonTests.FixturesToSkip.TryGetValue(fixture.Name)
          Skip.If(condition, reason) )

        output.WriteLine($"Fixture '%s{fixture.Name}'")
        let dagJsonCodec = DagJsonCodec()
        for fixtureEntry in fixture.Entries do
            match fixtureEntry.CodecName with
            | "dag-json" ->
                use dataStream = new MemoryStream(fixtureEntry.DataBytes)
                let dataModelNode = (dagJsonCodec :> ICodec).Decode(dataStream) |> Result.getOk

                output.WriteLine("Fixture bytes:")
                output.WriteLine(fixtureEntry.DataBytes.ToHexString())
                output.WriteLine("")
                output.WriteLine("Fixture text:")
                output.WriteLine(Encoding.UTF8.GetString(fixtureEntry.DataBytes))
                output.WriteLine("")
                output.WriteLine($"Fixture CID: {fixtureEntry.Cid}")
                output.WriteLine("")

                output.WriteLine("")

                use reencodedDataStream = new MemoryStream()
                (dagJsonCodec :> ICodec).Encode(reencodedDataStream, dataModelNode)
                let reencodedDataBytes = reencodedDataStream.ToArray()

                output.WriteLine("Reencoded bytes:")
                output.WriteLine(reencodedDataBytes.ToHexString())
                output.WriteLine("")
                output.WriteLine("Reencoded text:")
                output.WriteLine(Encoding.UTF8.GetString(reencodedDataBytes))
                output.WriteLine("")

                let reencodedMultiHash = MultiHash.ComputeHash(reencodedDataBytes, "sha2-256")
                let reencodedCid = Cid.create "base32" 1 "dag-json" reencodedMultiHash
                output.WriteLine($"Reencoded CID: {reencodedCid}")

                test <@ Cid.encode fixtureEntry.Cid = Cid.encode reencodedCid @>
            | "dag-cbor" ->
                // TODO: Implement
                ()
            | "dag-pb" ->
                // TODO: Implement
                ()
            | _ ->
                failwith $"Not supported codec: {fixtureEntry.CodecName}"
