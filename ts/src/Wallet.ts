//https://www.npmjs.com/package/ethereum-cryptography

import { keccak256 } from 'ethereum-cryptography/keccak';
import { utils, getPublicKey } from 'ethereum-cryptography/secp256k1';
import { toChecksumAddress } from 'ethereum-checksum-address';

import { Conversions } from './Conversions';

export class Wallet
{
    privateKey: Uint8Array;
    address: string;

    constructor(privateKey: string | null)
    {
        if (privateKey === null)
            this.privateKey = utils.randomPrivateKey();
        else
            this.privateKey = Conversions.fromHex(privateKey);

        let address: Uint8Array = keccak256(getPublicKey(this.privateKey).slice(1)).slice(12);
        this.address = toChecksumAddress(Conversions.toHex(address))
    }
}