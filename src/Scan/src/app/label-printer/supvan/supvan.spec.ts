import {buildDataFrames, checksum16, CMD, frameLen, makeCmd, parseMaterial, parseStatus, statusErrors} from './supvan-protocol';
import {lzmaCompress} from './supvan-lzma';
import {compressGrouped, calcSpeed, splitIntoBuffers, toColumnMajor, PRINT_BUF_SIZE} from './supvan-raster';
import {barcodeModuleDots, renderLabel} from './supvan-label-render';

describe('supvan protocol', () => {
  it('builds the CHECK_DEVICE frame like the reference implementation', () => {
    const frame = makeCmd(CMD.CHECK_DEVICE);
    expect(Array.from(frame.subarray(0, 8))).toEqual([0x7e, 0x5a, 0x0c, 0x00, 0x10, 0x01, 0xaa, 0x12]);
    expect(frame[11]).toBe(1);
    expect(frame[8]).toBe(1);
    expect(frame[9]).toBe(0);
  });

  it('puts both parameters little-endian and checksums bytes 10..16', () => {
    const frame = makeCmd(CMD.NEXT_ZIPPEDBULK, 512, 3);
    expect(Array.from(frame.subarray(12, 16))).toEqual([0x00, 0x02, 0x03, 0x00]);
    expect(frame[8] | (frame[9] << 8)).toBe(checksum16(frame.subarray(10, 16)));
  });

  it('frames data in 512-byte frames of 500-byte packets', () => {
    const frames = buildDataFrames(new Uint8Array(1100).fill(0x42));
    expect(frames.length).toBe(3);
    expect(frames[0].length).toBe(512);
    expect(Array.from(frames[0].subarray(0, 6))).toEqual([0x7e, 0x5a, 0xfc, 0x01, 0x10, 0x02]);
    expect(frames[0][6]).toBe(0xaa);
    expect(frames[0][7]).toBe(0xbb);
    expect(frames[2][6 + 4]).toBe(2);
    expect(frames[2][6 + 5]).toBe(3);
    const packet = frames[0].subarray(6);
    expect(packet[2] | (packet[3] << 8)).toBe(checksum16(packet.subarray(4)));
  });

  it('reads the frame length from the header', () => {
    expect(frameLen(Uint8Array.of(0x7e, 0x5a, 0x10, 0x00))).toBe(20);
    expect(frameLen(Uint8Array.of(0x00, 0x5a, 0x10, 0x00))).toBeNull();
  });

  it('decodes status registers', () => {
    const response = new Uint8Array(20);
    response[0] = 0x7e;
    response[1] = 0x5a;
    response[7] = CMD.INQUIRY_STA;
    response[14] = 0x02;
    response[16] = 0x08;
    response[17] = 0x01;
    response[18] = 5;
    const status = parseStatus(response);
    expect(status?.labelRwError).toBeTrue();
    expect(status?.coverOpen).toBeTrue();
    expect(status?.labelNotInstalled).toBeTrue();
    expect(status?.printCount).toBe(5);
    expect(statusErrors(status!).length).toBe(3);
    expect(parseStatus(new Uint8Array(10))).toBeNull();
  });

  it('decodes the loaded roll', () => {
    const response = new Uint8Array(22 + 25);
    response[0] = 0x7e;
    response[1] = 0x5a;
    response[7] = CMD.RETURN_MAT;
    response[22 + 18] = 40;
    response[22 + 19] = 30;
    response[22 + 20] = 3;
    response[22 + 21] = 100;
    const material = parseMaterial(response);
    expect(material?.widthMm).toBe(40);
    expect(material?.heightMm).toBe(30);
    expect(material?.gapMm).toBe(3);
    expect(material?.remaining).toBe(100);
  });
});

describe('supvan lzma', () => {
  it('writes the 13-byte header with props 0x5D, dict 8192 and the exact size', () => {
    const out = lzmaCompress(new Uint8Array(4096));
    expect(out[0]).toBe(0x5d);
    expect(Array.from(out.subarray(1, 5))).toEqual([0x00, 0x20, 0x00, 0x00]);
    expect(Array.from(out.subarray(5, 13))).toEqual([0x00, 0x10, 0, 0, 0, 0, 0, 0]);
    expect(out.length).toBeLessThan(400);
  });
});

describe('supvan raster', () => {
  it('maps image column x to head dot with LSB-first bytes, centred on 384 dots', () => {
    const width = 8;
    const ink = new Uint8Array(width * 2);
    ink[0] = 1; // ligne 0, x = 0 -> point 188
    ink[width + 7] = 1; // ligne 1, x = 7 -> point 195
    const {data, cols, bytesPerLine} = toColumnMajor({width, height: 2, ink});
    expect(cols).toBe(2);
    expect(bytesPerLine).toBe(48);
    expect(data[188 >> 3] & (1 << (188 & 7))).not.toBe(0);
    expect(data[48 + (195 >> 3)] & (1 << (195 & 7))).not.toBe(0);
  });

  it('builds flagged, checksummed print buffers', () => {
    const buffers = splitIntoBuffers({width: 320, height: 240, ink: new Uint8Array(320 * 240)});
    expect(buffers.length).toBe(3);
    expect(buffers[0].length).toBe(PRINT_BUF_SIZE);
    expect(buffers[0][2] & 0x02).toBe(0x02);
    expect(buffers[0][2] & 0x04).toBe(0);
    expect(buffers[2][2] & 0x0c).toBe(0x0c);
    expect(buffers[0][6]).toBe(48);
    expect(buffers[0][3] >> 6).toBe(1);
  });

  it('maps average block size to vendor speeds', () => {
    expect(calcSpeed(4000)).toBe(10);
    expect(calcSpeed(1000)).toBe(55);
    expect(calcSpeed(100)).toBe(60);
  });

  it('renders a barcode label that compresses into one block', () => {
    const image = renderLabel({code: '2000000000008', price: '12,50 €', widthMm: 40, heightMm: 30});
    expect(image.width).toBe(320);
    expect(image.height).toBe(240);
    expect(image.ink.some(value => value === 1)).toBeTrue();
    const page = compressGrouped(splitIntoBuffers(image));
    expect(page.blocks.every(block => block.data.length <= 4096)).toBeTrue();
    expect(barcodeModuleDots(40)).toBe(2);
  });
});
