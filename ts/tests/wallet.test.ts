import { keccak256 } from 'ethereum-cryptography/keccak';
import { Conversions } from '../src/Conversions';
import { Wallet } from '../src/Wallet';
import { Signer } from '../src/Signer';
import { PRIVATE_KEY, ADDRESS, TEST_DATA, TEST_DATA_HASH, TEST_DATA_SIG } from './TestData'

test('Hash', () => {
    let hash = keccak256(Conversions.fromHex(TEST_DATA));
    expect(Conversions.toHexNonPrefixed(hash)).toBe(TEST_DATA_HASH)
});

test('Create wallet', () => {
    let wallet: Wallet = new Wallet(PRIVATE_KEY);
    expect(wallet.address).toBe(ADDRESS)
});

test('Sign', () => {
    let wallet: Wallet = new Wallet(PRIVATE_KEY);
    var sig: string = Signer.hashAndSign(wallet, Conversions.fromHex(TEST_DATA));
    expect(sig).toBe(TEST_DATA_SIG);
});

test('Recover', () => {
    let wallet: Wallet = new Wallet(PRIVATE_KEY);
    var sig: string = Signer.hashAndSign(wallet, Conversions.fromHex(TEST_DATA));
    var recovered: string = Signer.hashAndRecover(Conversions.fromHex(TEST_DATA), sig);
    expect(recovered).toBe(ADDRESS);
});