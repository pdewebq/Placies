namespace Placies.Gateways.SigningProxyGateway

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
open Placies.Gateways
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

    // RSA

    let signContentRootRsa (contentRoot: IpfsContentRoot) (rsaPrivateKey: RsaKeyParameters) (hashAlg: HashAlgorithmName) =
        let stream = new MemoryStream()
        use streamWriter = new BinaryWriter(stream)
        stream.WriteVarInt(MultiCodecInfos.Varsig.Code)
        stream.WriteVarInt(MultiCodecInfos.RsaPub.Code)
        match hashAlg with
        | Equals HashAlgorithmName.SHA256 ->
            let rsaSigner = RsaDigestSigner(Sha256Digest())
            rsaSigner.Init(true, rsaPrivateKey)

            stream.WriteVarInt(MultiCodecInfos.Sha2_256.Code)
            match contentRoot with
            | IpfsContentRoot.Ipfs cid ->
                let signingDataBytes = cid |> Cid.toByteArray
                let signatureBytes = rsaSigner.GenerateSignatureFromData(signingDataBytes)
                stream.WriteVarInt(signatureBytes.Length)
                stream.WriteVarInt(MultiCodecInfos.Ipfs.Code)
                stream.WriteVarInt(MultiCodecInfos.Cidv1.Code)
                streamWriter.Write(signatureBytes)
            | IpfsContentRoot.Ipns (IpfsContentRootIpns.Key libp2pKey) ->
                let signingDataBytes = libp2pKey |> Cid.toByteArray
                let signatureBytes = rsaSigner.GenerateSignatureFromData(signingDataBytes)
                stream.WriteVarInt(signatureBytes.Length)
                stream.WriteVarInt(MultiCodecInfos.Ipns.Code)
                stream.WriteVarInt(MultiCodecInfos.Libp2pKey.Code)
                streamWriter.Write(signatureBytes)
            | IpfsContentRoot.Ipns (IpfsContentRootIpns.DnsName dnsName) ->
                let signingDataBytes = Encoding.UTF8.GetBytes(dnsName)
                let signatureBytes = rsaSigner.GenerateSignatureFromData(signingDataBytes)
                stream.WriteVarInt(signatureBytes.Length)
                stream.WriteVarInt(MultiCodecInfos.Ipns.Code)
                stream.WriteVarInt(MultiCodecInfos.Dns.Code)
                streamWriter.Write(signatureBytes)
        | _ ->
            invalidOp $"Not supported hash algorithm: {hashAlg}"
        stream

    let verifyVarsigSignatureRsa (multibaseProvider: IMultiBaseProvider) (contentRoot: IpfsContentRoot) (rsaPublicKey: RsaKeyParameters) (varsigStr: string) = result {
        let varsigBytes = MultiBase.tryDecode multibaseProvider varsigStr |> Result.getOk
        use stream = new MemoryStream(varsigBytes)
        let varsigCode = stream.ReadVarIntAsInt32()
        do! Result.requireEqual varsigCode MultiCodecInfos.Varsig.Code $"{varsigCode} is not varsig"
        let varsigHeaderCode = stream.ReadVarIntAsInt32()
        match varsigHeaderCode with
        | Equals MultiCodecInfos.RsaPub.Code ->
            let rsaHashAlgorithmCode = stream.ReadVarIntAsInt32()
            match rsaHashAlgorithmCode with
            | Equals MultiCodecInfos.Sha2_256.Code ->
                let rsaSigner = RsaDigestSigner(Sha256Digest())
                rsaSigner.Init(false, rsaPublicKey)

                let signatureByteLength = stream.ReadVarIntAsInt32()
                let readSigningDataBytes (stream: Stream) = result {
                    match contentRoot with
                    | IpfsContentRoot.Ipfs cid ->
                        do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Ipfs.Code |> Result.requireTrue "Signature is not for /ipfs"
                        do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Cidv1.Code |> Result.requireTrue "Signature is not for /ipfs/cidv1"
                        let cidBytes = cid |> Cid.toByteArray
                        return cidBytes
                    | IpfsContentRoot.Ipns (IpfsContentRootIpns.Key libp2pKey) ->
                        do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Ipns.Code |> Result.requireTrue "Signature is not for /ipns"
                        do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Libp2pKey.Code |> Result.requireTrue "Signature is not for /ipns/libp2p-key"
                        let libp2pKeyBytes = libp2pKey |> Cid.toByteArray
                        return libp2pKeyBytes
                    | IpfsContentRoot.Ipns (IpfsContentRootIpns.DnsName dnsName) ->
                        do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Ipns.Code |> Result.requireTrue "Signature is not for /ipns"
                        do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Dns.Code |> Result.requireTrue "Signature is not for /ipns/dns"
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

    // Ed25519

    let signContentRootEd25519 (contentRoot: IpfsContentRoot) (ed25519PrivateKey: Ed25519PrivateKeyParameters) =
        let ed25519Signer = Ed25519Signer()
        ed25519Signer.Init(true, ed25519PrivateKey)

        let stream = new MemoryStream()
        use streamWriter = new BinaryWriter(stream)
        stream.WriteVarInt(MultiCodecInfos.Varsig.Code)
        stream.WriteVarInt(MultiCodecInfos.Ed25519Pub.Code)
        match contentRoot with
        | IpfsContentRoot.Ipfs cid ->
            let signingDataBytes = cid |> Cid.toByteArray
            let signatureBytes = ed25519Signer.GenerateSignatureFromData(signingDataBytes)
            stream.WriteVarInt(signatureBytes.Length)
            stream.WriteVarInt(MultiCodecInfos.Ipfs.Code)
            stream.WriteVarInt(MultiCodecInfos.Cidv1.Code)
            streamWriter.Write(signatureBytes)
        | IpfsContentRoot.Ipns (IpfsContentRootIpns.Key libp2pKey) ->
            let signingDataBytes = libp2pKey |> Cid.toByteArray
            let signatureBytes = ed25519Signer.GenerateSignatureFromData(signingDataBytes)
            stream.WriteVarInt(signatureBytes.Length)
            stream.WriteVarInt(MultiCodecInfos.Ipns.Code)
            stream.WriteVarInt(MultiCodecInfos.Libp2pKey.Code)
            streamWriter.Write(signatureBytes)
        | IpfsContentRoot.Ipns (IpfsContentRootIpns.DnsName dnsName) ->
            let signingDataBytes = Encoding.UTF8.GetBytes(dnsName)
            let signatureBytes = ed25519Signer.GenerateSignatureFromData(signingDataBytes)
            stream.WriteVarInt(signatureBytes.Length)
            stream.WriteVarInt(MultiCodecInfos.Ipns.Code)
            stream.WriteVarInt(MultiCodecInfos.Dns.Code)
            streamWriter.Write(signatureBytes)
        stream

    let verifyVarsigSignatureEd25519 (multibaseProvider: IMultiBaseProvider) (contentRoot: IpfsContentRoot) (ed25519PublicKey: Ed25519PublicKeyParameters) (varsigStr: string) = result {
        let ed25519Signer = Ed25519Signer()
        ed25519Signer.Init(false, ed25519PublicKey)

        let varsigBytes = varsigStr |> MultiBase.decode multibaseProvider
        use stream = new MemoryStream(varsigBytes)
        let varsigCode = stream.ReadVarIntAsInt32()
        do! Result.requireEqual varsigCode MultiCodecInfos.Varsig.Code $"{varsigCode} is not varsig"
        let varsigHeaderCode = stream.ReadVarIntAsInt32()
        match varsigHeaderCode with
        | Equals MultiCodecInfos.Ed25519Pub.Code ->
            let signatureByteLength = stream.ReadVarIntAsInt32()
            let readSigningDataBytes (stream: Stream) = result {
                match contentRoot with
                | IpfsContentRoot.Ipfs cid ->
                    do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Ipfs.Code |> Result.requireTrue "Signature is not for /ipfs"
                    do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Cidv1.Code |> Result.requireTrue "Signature is not for /ipfs/cidv1"
                    let cidBytes = cid |> Cid.toByteArray
                    return cidBytes
                | IpfsContentRoot.Ipns (IpfsContentRootIpns.Key libp2pKey) ->
                    do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Ipns.Code |> Result.requireTrue "Signature is not for /ipns"
                    do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Libp2pKey.Code |> Result.requireTrue "Signature is not for /ipns/libp2p-key"
                    let libp2pKeyBytes = libp2pKey |> Cid.toByteArray
                    return libp2pKeyBytes
                | IpfsContentRoot.Ipns (IpfsContentRootIpns.DnsName dnsName) ->
                    do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Ipns.Code |> Result.requireTrue "Signature is not for /ipns"
                    do! stream.ReadVarIntAsInt32() = MultiCodecInfos.Dns.Code |> Result.requireTrue "Signature is not for /ipns/dns"
                    let dnsNameBytes = Encoding.UTF8.GetBytes(dnsName)
                    return dnsNameBytes
            }
            let! signingDataBytes = readSigningDataBytes stream
            let signatureBytes = Array.zeroCreate signatureByteLength
            stream.Read(signatureBytes.AsSpan()) |> ignore
            let isValid = ed25519Signer.VerifySignatureFromData(signingDataBytes, signatureBytes)
            return isValid
        | _ ->
            return! Error $"Not supported signature header: {varsigHeaderCode}"
    }
