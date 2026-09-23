export type SelectionRef =
  | {kind: 'edition'; isbn13: string}
  | {kind: 'rare'; rareBookId: string};

export interface LocalSelectionEntry {
  ref: SelectionRef;
  title: string;
  addedAt: string;
}

export function selectionKey(ref: SelectionRef): string {
  return ref.kind === 'edition'
    ? `edition:${ref.isbn13}`
    : `rare:${ref.rareBookId}`;
}

export function planSelectionMerge(
  local: readonly LocalSelectionEntry[],
  remoteKeys: ReadonlySet<string>,
): {toSend: LocalSelectionEntry[]; alreadyRemote: number; needsConfirmation: boolean} {
  const toSend = local.filter(entry => !remoteKeys.has(selectionKey(entry.ref)));
  return {
    toSend,
    alreadyRemote: local.length - toSend.length,
    needsConfirmation: local.length > 0 && remoteKeys.size > 0,
  };
}
