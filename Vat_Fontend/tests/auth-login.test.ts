import '@angular/compiler';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { signal } from '@angular/core';
import { firstValueFrom, of, throwError } from 'rxjs';
import { AuthService } from '../src/app/core/services/auth.service';

function setup() {
  const storage = new Map<string, string>();
  Object.defineProperty(globalThis, 'sessionStorage', { configurable: true, value: {
    getItem: (key: string) => storage.get(key) ?? null,
    setItem: (key: string, value: string) => storage.set(key, value),
    removeItem: (key: string) => storage.delete(key)
  }});
  const auth = Object.assign(Object.create(AuthService.prototype), {
    ready: Promise.resolve(), currentUser: signal(null), token: signal(null),
    isAuthenticated: signal(false), expiresAt: 0,
    contextService: { currentUser: signal(null), loadVerifiedCompanies: async () => {}, clearVerifiedContext: () => {} }
  }) as AuthService;
  return { auth, storage };
}

test('login uses one API prefix and verifies the issued token before accepting the session', async () => {
  const original = globalThis.fetch;
  const { auth, storage } = setup();
  const urls: string[] = [];
  globalThis.fetch = async (input, init) => {
    urls.push(String(input));
    if (String(input).endsWith('/auth/login')) return Response.json({ accessToken: 'signed-token', expiresIn: 3600 });
    assert.equal((init?.headers as Record<string, string>).Authorization, 'Bearer signed-token');
    return Response.json({ subject: 'verified-actor', tenantId: 'tenant', companyIds: ['company'], permissions: ['purchase.read'], roles: ['Admin'], username: 'imran', displayName: 'Imran Hossain' });
  };
  try {
    const account = await firstValueFrom(auth.login({ username: 'admin', password: 'valid-password' }));
    assert.deepEqual(urls, ['/api/v1/auth/login', '/api/v1/identity/me']);
    assert.equal(account.id, 'verified-actor');
    assert.equal(account.role, 'SuperAdmin');
    assert.equal(account.username, 'imran');
    assert.equal(account.fullName, 'Imran Hossain');
    assert.equal(auth.hasValidSession(), true);
    assert.equal(storage.get('vat_secure_token'), 'signed-token');
  } finally { globalThis.fetch = original; }
});

for (const scenario of ['wrong password', 'offline', 'rejected token']) {
  test(`login fails without a demo session when ${scenario}`, async () => {
    const original = globalThis.fetch;
    const { auth, storage } = setup();
    globalThis.fetch = async input => {
      if (scenario === 'offline') throw new TypeError('fetch failed');
      if (scenario === 'wrong password' || String(input).endsWith('/identity/me')) return new Response(null, { status: 401 });
      return Response.json({ accessToken: 'untrusted', expiresIn: 3600 });
    };
    try {
      await assert.rejects(firstValueFrom(auth.login({ username: 'admin', password: 'wrong' })));
      assert.equal(auth.hasValidSession(), false);
      assert.equal(auth.currentUser(), null);
      assert.equal(storage.has('vat_secure_token'), false);
    } finally { globalThis.fetch = original; }
  });
}

for (const accepted of [true, false]) {
  test(`session restore revalidates stored tokens and ${accepted ? 'uses server grants' : 'clears rejected sessions'}`, async () => {
    const original = globalThis.fetch;
    const { auth, storage } = setup();
    storage.set('vat_secure_token', 'stored-token');
    storage.set('vat_secure_expires', String(Date.now() + 3600000));
    storage.set('vat_secure_user', JSON.stringify({ permissions: ['identity.manage'] }));
    Object.defineProperty(globalThis, 'location', { configurable: true, value: new URL('https://vat.example/login') });
    (auth as any).manager = async () => ({ getUser: async () => null });
    globalThis.fetch = async input => {
      assert.equal(String(input), '/api/v1/identity/me');
      return accepted ? Response.json({ subject: 'verified', tenantId: 'tenant', companyIds: [], permissions: ['report.read'] })
        : new Response(null, { status: 401 });
    };
    try {
      await (auth as any).restore();
      assert.equal(auth.hasValidSession(), accepted);
      if (accepted) assert.deepEqual(auth.currentUser()?.permissions, ['report.read']);
      else assert.equal(storage.has('vat_secure_token'), false);
    } finally { globalThis.fetch = original; }
  });
}

test('user registration uses the scoped database API and preserves the entered password', async () => {
  const { auth } = setup();
  const request = { username: 'new.maker', password: ' Password!123 ', companyId: 'company', roles: ['Maker'], assignedModuleCodes: ['MOD_PURCHASES'] };
  const calls: unknown[] = [];
  (auth as any).http = { post: (url: string, body: unknown) => { calls.push([url, body]); return of({ id: 'persisted-user' }); } };
  const result = await firstValueFrom(auth.register(request as any));
  assert.equal(result.id, 'persisted-user');
  assert.deepEqual(calls, [['/api/v1/companies/company/identity/users', request]]);
});

test('user registration failures propagate without creating a browser account', async () => {
  const { auth } = setup();
  (auth as any).http = { post: () => throwError(() => new Error('Username already exists')) };
  await assert.rejects(firstValueFrom(auth.register({ companyId: 'company' } as any)), /Username already exists/);
  assert.equal(auth.currentUser(), null);
});
