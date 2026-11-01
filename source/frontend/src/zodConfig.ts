import { z } from 'zod';

// The content security policy forbids eval (M5-D, #38). Zod would otherwise probe `new Function` on its first object
// schema, which the browser reports as a violation; jitless validation needs no eval and gives the same results.
z.config({ jitless: true });
