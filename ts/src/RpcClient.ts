import axios from 'axios';

export class RpcClient
{
    url: string

    constructor(url: string) {
        this.url = url;
    }

    post = async (data: any): Promise<{status: number, data: string | null}> => {

        try {
            let rpcRequest = {
                api: {
                    major: 1,
                    minor: 0
                },
                data: data
            }
            let result = await axios.post(this.url, rpcRequest);
            return new Promise<{status: number, data: string | null}>((resolve) => { resolve({
                status: result.status,
                data: result.data
            }); });
        }
        catch (error) {
            if (error.response)
                return new Promise<{status: number, data: string | null}>((resolve) => { resolve({ status: error.response.status, data: error.response.data }); });
            else
                return new Promise<{status: number, data: string | null}>((resolve) => { resolve({ status: -1, data: null }); });
        }
    }
}