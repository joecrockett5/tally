import { describe, expect, it } from 'vitest';
import { food } from '../test/fixtures';
import { mergeResults } from './AddFoodPage';

describe('mergeResults', () => {
  it('keeps local results first and drops online duplicates by barcode', () => {
    const mine = food({ id: 'mine', source: 'mine', barcode: '5000000000011' });
    const offDupe = food({ id: 'off1', source: 'off', barcode: '5000000000011' });
    const offNew = food({ id: 'off2', source: 'off', barcode: '5000000000028' });
    expect(mergeResults([mine], [offDupe, offNew]).map((f) => f.id)).toEqual(['mine', 'off2']);
  });
});
