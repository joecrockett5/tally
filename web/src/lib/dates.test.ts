import { describe, expect, it } from 'vitest';
import { addDays, endOfMonth, formatDayLabel, formatUkDate, startOfWeek, todayIn } from './dates';

describe('dates', () => {
  it('computes today in the server timezone, not the device one', () => {
    // 23:30 UTC on 29 March 2026 is 00:30 BST on 30 March in London.
    const instant = new Date('2026-03-29T23:30:00Z');
    expect(todayIn('Europe/London', instant)).toBe('2026-03-30');
    expect(todayIn('UTC', instant)).toBe('2026-03-29');
  });
  it('adds days across month ends and DST changes', () => {
    expect(addDays('2026-03-28', 1)).toBe('2026-03-29');
    expect(addDays('2026-03-29', 1)).toBe('2026-03-30');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
  });
  it('formats UK dates', () => {
    expect(formatUkDate('2026-10-06')).toBe('06/10/2026');
    expect(formatDayLabel('2026-10-05')).toBe('Mon 05/10/2026');
  });
  it('starts weeks on Monday', () => {
    expect(startOfWeek('2026-10-18')).toBe('2026-10-12'); // Sunday → previous Monday
    expect(startOfWeek('2026-10-12')).toBe('2026-10-12');
  });
  it('finds month ends', () => {
    expect(endOfMonth('2028-02-10')).toBe('2028-02-29');
  });
});
