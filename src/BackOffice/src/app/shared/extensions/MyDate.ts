export class MyDate extends Date {
  toISOUtcString() {
    const userTimezoneOffset = this.getTimezoneOffset() * 60000;
    return new Date(this.getTime() - userTimezoneOffset).toISOString();
  }
}
