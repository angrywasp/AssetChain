export class Conversions {
    static toByte = (value: number): Uint8Array => {
        let dataLength: number = 0;

        if (value <= 155)
            dataLength = 1;
        else if (value <= 65535)
            dataLength = 2;
        else if (value <= 4294967295)
            dataLength = 4;
        else if (value <= 18446744073709551615)
            dataLength = 8;
        else
            console.error("Number is too large to be expressed as hex");

        let a = new Uint8Array(dataLength);
        for (let i = 0; i < dataLength; i++)
            a[i] = value >> (8 * i);

        return a;
    }

    static toByteHex = (value: number, overallLength: number): string => {
        var a = this.toByte(value);
        return this.toHexNonPrefixed(a).padEnd(overallLength * 2, '0');
    }

    static toHex = (value: Uint8Array): string => {
        return '0x' + Buffer.from(value).toString('hex');
    }

    static toHexNonPrefixed = (value: Uint8Array): string => {
        return Buffer.from(value).toString('hex');
    }

    static removeHexPrefix = (value: string): string => {
        if (value.startsWith('0x'))
            return value.slice(2);

        return value;
    }

    static fromHex = (value: string): Uint8Array => {
        if (value.startsWith('0x'))
            value = value.slice(2);

        return Uint8Array.from(Buffer.from(value, 'hex'));
    }
}