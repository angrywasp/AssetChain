import { keccak256 } from 'ethereum-cryptography/keccak';
import { recoverPublicKey } from 'ethereum-cryptography/secp256k1';
import secp256k1 from 'secp256k1';
import { toChecksumAddress } from 'ethereum-checksum-address';
import { Conversions } from './Conversions';
import { CHAIN_ID } from './Constants';
import { Wallet } from './Wallet';

export interface ISignature
{
    signature: Uint8Array;
    recId: Number
}

export class Signer {
    static hashAndSign = (account: Wallet, data: Uint8Array): string => {
        let hash: Uint8Array = keccak256(data);
        let sig = secp256k1.ecdsaSign(hash, account.privateKey);
        
        return Conversions.toHexNonPrefixed(sig.signature) + this.#calculateV(sig.recid);
    }

    static sign = (account: Wallet, hash: Uint8Array): string => {
        let sig = secp256k1.ecdsaSign(hash, account.privateKey)

        return Conversions.toHexNonPrefixed(sig.signature) + this.#calculateV(sig.recid);
    }

    static hashAndRecover = (data: Uint8Array, signature: string): string => {
        let splitSig: { signature: Uint8Array, recId: number } = this.#recoverRecId(signature);

        let hash: Uint8Array = keccak256(data);
        let pubKey: Uint8Array = recoverPublicKey(hash, splitSig.signature, splitSig.recId);

        return toChecksumAddress(Conversions.toHex(keccak256(pubKey.slice(1)).slice(12)));
    }

    static #calculateV = (recId: number): string => {
        let v = recId + (CHAIN_ID * 2 + 35);
        let vBytes = Conversions.toByte(v);

        if (v <= 255)
            return vBytes[0].toString(16);
        else
            return vBytes[1].toString(16).padStart(2, '0') + vBytes[0].toString(16).padStart(2, '0');
    }

    static #recoverRecId = (sig: string): {signature: Uint8Array, recId: number} => {
        let sBytes: Uint8Array = Conversions.fromHex(sig);
        let vBytes = sBytes.subarray(64);

        let v = 0;
        if (vBytes.byteLength === 1)
            v = vBytes[0];
        else
            v = vBytes[0] << 8 | vBytes[1];

        let r = v - (CHAIN_ID * 2 + 35);

        return {
            signature: sBytes.subarray(0, 64),
            recId: r
        };
    }
}