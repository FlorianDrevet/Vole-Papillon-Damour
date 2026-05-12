export enum NumberLineEnum {
  ONELINE = 'OneLine',
  TWOLINE = 'TwoLine',
  CARTONPLEIN = 'CartonPlein',
}

export function compareNumberLines(a: NumberLineEnum, b: NumberLineEnum): number {
  const order = {
    [NumberLineEnum.ONELINE]: 1,
    [NumberLineEnum.TWOLINE]: 2,
    [NumberLineEnum.CARTONPLEIN]: 3,
  };

  return order[a] - order[b];
}
