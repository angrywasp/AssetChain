import { keccak256 } from 'ethereum-cryptography/keccak';
import { CHAIN_ID, EMPTY_ADDRESS, TX_VERSION } from "./Constants";
import { Conversions } from "./Conversions";
import { Signer } from "./Signer";
import { Wallet } from "./Wallet";

export enum Transaction_Type
{
    Invalid,
    Transfer,
    AddValidator,
    RemoveValidator,
    Max
}

export class Transaction {
    version: number = 0;
    type: Transaction_Type = Transaction_Type.Invalid;
    fee: number = 0;
    nonce: number = 0;
    from: string = EMPTY_ADDRESS;
    to: string = EMPTY_ADDRESS;
    data: string = '';
    hash: string;
    signature: string;

    constructor() {

    }

    static createTransfer = (account: Wallet, nonce: number, to: string, amount: number): Transaction => {
        return this.create(account, Transaction_Type.Transfer, nonce, to, Conversions.toByteHex(amount, 8));
    };

    static create = (account: Wallet, type: Transaction_Type, nonce: number, to: string, data: string): Transaction => {
        let tx:Transaction = new Transaction();
        tx.version = TX_VERSION;
        tx.type = type;
        tx.nonce = nonce;
        tx.from = account.address;
        tx.to = to;
        tx.data = data;
        tx.fee = this.calculateFee(type);

        let hash: Uint8Array = this.#getHash(tx);

        tx.hash = Conversions.toHexNonPrefixed(hash);
        tx.signature = Signer.sign(account, hash);
        return tx;
    }

    static calculateFee = (type: Transaction_Type): number => {
        switch (type)
        {
            case Transaction_Type.Transfer:
                return 100;
            case Transaction_Type.AddValidator:
            case Transaction_Type.RemoveValidator:
                return 500;
            default:
                console.error('Invalid transaction type. Fee calculation failed.');
                return 18446744073709551615;
        }
    }

    static #getHash = (tx: Transaction): Uint8Array => {
        var txData:string = '';
        txData += Conversions.toByteHex(tx.version, 2);
        txData += Conversions.toByteHex(tx.type, 1);
        txData += Conversions.toByteHex(tx.nonce, 4);
        txData += Conversions.removeHexPrefix(tx.from);
        txData += Conversions.removeHexPrefix(tx.to);
        txData += Conversions.removeHexPrefix(tx.data);
        txData += Conversions.toByteHex(tx.fee, 8);

        return keccak256(Conversions.fromHex(txData));
    }
}