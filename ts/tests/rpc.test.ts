import { Transaction, Transaction_Type } from '../src/Transaction';
import { Wallet } from '../src/Wallet';
import { RpcClient } from '../src/RpcClient';

test('RPC', async () => {
    let wallet: Wallet = new Wallet(PRIVATE_KEY);

    let result = await new RpcClient('http://127.0.0.1:6001/nonce').post(ADDRESS);
    expect(result.status).toBe(200);

    let tx = Transaction.createTransfer(wallet, parseInt(result.data), '0x18490f2953431E31dEA6Ec8bE901ccDAbdCEb279', 1000000);
    result = await new RpcClient('http://127.0.0.1:6001/transfer').post(tx);
    expect(result.status).toBe(200);

    console.log(result.data);
});
