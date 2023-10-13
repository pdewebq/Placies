namespace Placies.SigningGateway

open System
open System.IO
open System.Security.Cryptography
open System.Text
open FsToolkit.ErrorHandling
open Org.BouncyCastle.Crypto
open Org.BouncyCastle.Crypto.Digests
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.Crypto.Signers
open Placies
open Placies.Utils
open Placies.Gateway
open Placies.Multiformats

[<AutoOpen>]
module SignerExtensions =
    type ISigner with
        member this.GenerateSignatureFromData(input: byte array): byte array =
            this.BlockUpdate(input, 0, input.Length)
            let signature = this.GenerateSignature()
            this.Reset()
            signature
        member this.VerifySignatureFromData(input: byte array, signature: byte array): bool =
            this.BlockUpdate(input, 0, input.Length)
            let isValid = this.VerifySignature(signature)
            this.Reset()
            isValid

[<RequireQualifiedAccess>]
module SigningContentRoot =

    let signContentRootRsa (contentRoot: IpfsContentRoot) (rsaPrivateKey: RsaKeyParameters) (hashAlg: HashAlgorithmName) =
        let stream = new MemoryStream()
        use streamWriter = new BinaryWriter(stream)
        stream.WriteVarint(MultiCodecInfos.Varsig.Code)
        stream.WriteVarint(MultiCodecInfos.RsaPub.Code)
        match hashAlg with
        | Equals HashAlgorithmName.SHA256 ->
            let rsaSigner = RsaDigestSigner(Sha256Digest())
            rsaSigner.Init(true, rsaPrivateKey)

            stream.WriteVarint(MultiCodecInfos.Sha2_256.Code)
            match contentRoot with
            | IpfsContentRoot.Ipfs cid ->
                let signingDataBytes = cid |> Cid.toByteArray
                let signatureBytes = rsaSigner.GenerateSignatureFromData(signingDataBytes)
                stream.WriteVarint(signatureBytes.Length)
                stream.WriteVarint(MultiCodecInfos.Ipfs.Code)
                stream.WriteVarint(MultiCodecInfos.Cidv1.Code)
                streamWriter.Write(signatureBytes)
            | IpfsContentRoot.Ipns (IpfsContentRootIpns.Key libp2pKey) ->
                let signingDataBytes = libp2pKey |> Cid.toByteArray
                let signatureBytes = rsaSigner.GenerateSignatureFromData(signingDataBytes)
                stream.WriteVarint(signatureBytes.Length)
                stream.WriteVarint(MultiCodecInfos.Ipns.Code)
                stream.WriteVarint(MultiCodecInfos.Libp2pKey.Code)
                streamWriter.Write(signatureBytes)
            | IpfsContentRoot.Ipns (IpfsContentRootIpns.DnsName dnsName) ->
                let signingDataBytes = Encoding.UTF8.GetBytes(dnsName)
                let signatureBytes = rsaSigner.GenerateSignatureFromData(signingDataBytes)
                stream.WriteVarint(signatureBytes.Length)
                stream.WriteVarint(MultiCodecInfos.Ipns.Code)
                stream.WriteVarint(MultiCodecInfos.Dns.Code)
                streamWriter.Write(signatureBytes)
        | _ ->
            invalidOp $"Not supported hash algorithm: {hashAlg}"
        stream

    let verifyVarsigSignature (multibaseProvider: IMultiBaseProvider) (contentRoot: IpfsContentRoot) (rsaPublicKey: RsaKeyParameters) (varsigStr: string) = result {
        let varsigBytes = MultiBase.tryDecode multibaseProvider varsigStr |> Result.getOk
        use stream = new MemoryStream(varsigBytes)
        let varsigCode = stream.ReadVarint32()
        do! Result.requireEqual varsigCode MultiCodecInfos.Varsig.Code $"{varsigCode} is not varsig"
        let varsigHeaderCode = stream.ReadVarint32()
        match varsigHeaderCode with
        | Equals MultiCodecInfos.RsaPub.Code ->
            let rsaHashAlgorithmCode = stream.ReadVarint32()
            match rsaHashAlgorithmCode with
            | Equals MultiCodecInfos.Sha2_256.Code ->
                let rsaSigner = RsaDigestSigner(Sha256Digest())
                rsaSigner.Init(false, rsaPublicKey)

                let signatureByteLength = stream.ReadVarint32()
                let readSigningDataBytes (stream: Stream) = result {
                    match contentRoot with
                    | IpfsContentRoot.Ipfs cid ->
                        do! stream.ReadVarint32() = MultiCodecInfos.Ipfs.Code |> Result.requireTrue "Signature is not for /ipfs"
                        do! stream.ReadVarint32() = MultiCodecInfos.Cidv1.Code |> Result.requireTrue "Signature is not for /ipfs/cidv1"
                        let cidBytes = cid |> Cid.toByteArray
                        return cidBytes
                    | IpfsContentRoot.Ipns (IpfsContentRootIpns.Key libp2pKey) ->
                        do! stream.ReadVarint32() = MultiCodecInfos.Ipns.Code |> Result.requireTrue "Signature is not for /ipns"
                        do! stream.ReadVarint32() = MultiCodecInfos.Libp2pKey.Code |> Result.requireTrue "Signature is not for /ipns/libp2p-key"
                        let libp2pKeyBytes = libp2pKey |> Cid.toByteArray
                        return libp2pKeyBytes
                    | IpfsContentRoot.Ipns (IpfsContentRootIpns.DnsName dnsName) ->
                        do! stream.ReadVarint32() = MultiCodecInfos.Ipns.Code |> Result.requireTrue "Signature is not for /ipns"
                        do! stream.ReadVarint32() = MultiCodecInfos.Dns.Code |> Result.requireTrue "Signature is not for /ipns/dns"
                        let dnsNameBytes = Encoding.UTF8.GetBytes(dnsName)
                        return dnsNameBytes
                }
                let! signingDataBytes = readSigningDataBytes stream
                let signatureBytes = Array.zeroCreate signatureByteLength
                stream.Read(signatureBytes.AsSpan()) |> ignore
                let isValid = rsaSigner.VerifySignatureFromData(signingDataBytes, signatureBytes)
                return isValid
            | _ ->
                return! Error $"Not supported hash algorithm: {rsaHashAlgorithmCode}"
        | _ ->
            return! Error $"Not supported signature header: {varsigHeaderCode}"
    }
