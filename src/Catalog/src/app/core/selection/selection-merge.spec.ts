import {planSelectionMerge, SelectionRef, selectionKey} from './selection-merge';

describe('planSelectionMerge', () => {
  const local = (isbn13: string) => ({
    ref: {kind: 'edition' as const, isbn13},
    title: 't',
    addedAt: '2026-09-20T10:00:00.000Z'
  });

  it('sends only entries missing remotely and asks confirmation when both lists are non-empty', () => {
    const plan = planSelectionMerge([local('1'), local('2')], new Set(['edition:2', 'edition:3']));

    expect(plan.toSend.map(e => selectionKey(e.ref))).toEqual(['edition:1']);
    expect(plan.alreadyRemote).toBe(1);
    expect(plan.needsConfirmation).toBeTrue();
  });

  it('does not ask confirmation when the account is empty', () => {
    expect(planSelectionMerge([local('1')], new Set()).needsConfirmation).toBeFalse();
  });

  it('does nothing when the local list is empty', () => {
    const plan = planSelectionMerge([], new Set(['edition:3']));

    expect(plan.toSend).toEqual([]);
    expect(plan.needsConfirmation).toBeFalse();
  });

  it('keys rare selection references by their exact identifier', () => {
    const ref: SelectionRef = {kind: 'rare', rareBookId: 'rare-123'};

    expect(selectionKey(ref)).toBe('rare:rare-123');
  });
});
