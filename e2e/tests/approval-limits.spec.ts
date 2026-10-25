import { expect, test } from '@playwright/test';

async function signIn(page: any, username: string, password: string) {
  await page.goto('/');
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible();
}

async function createAndSubmitInvoice(page: any, username: string, customerName: string, amount: string) {
  await signIn(page, username, 'ChangeMe123!');

  await page.getByLabel('Customer name').fill(customerName);
  await page.getByLabel('Amount').fill(amount);
  await page.getByRole('button', { name: 'Create invoice' }).click();

  const invoiceRow = page.getByRole('row').filter({ hasText: customerName });
  await expect(invoiceRow).toContainText('Draft');
  await invoiceRow.getByRole('button', { name: /Submit invoice/i }).click();
}

async function assertNoDecisionError(page: any) {
  await expect(page.locator('#decision-error')).toBeHidden();
}

async function assertInvoicePending(page: any, customerName: string) {
  await page.getByRole('tab', { name: 'Invoices' }).click();
  await expect(page.getByRole('row').filter({ hasText: customerName })).toContainText('Pending Approval');
}

async function approveFromApprovals(page: any, customerName: string) {
  await page.getByRole('tab', { name: 'Approvals' }).click();
  const approvalItem = page.getByRole('listitem').filter({ hasText: customerName });
  await expect(approvalItem).toBeVisible();
  await expect(approvalItem.getByRole('button', { name: /Approve invoice/i })).toBeVisible();
  await approvalItem.getByRole('button', { name: /Approve invoice/i }).click();
}

async function expectApprovalFailure(page: any, customerName: string, pattern: RegExp) {
  await page.getByRole('tab', { name: 'Approvals' }).click();
  const approvalItem = page.getByRole('listitem').filter({ hasText: customerName });
  await expect(approvalItem).toBeVisible();
  await approvalItem.getByRole('button', { name: /Approve invoice/i }).click();
  await expect(page.getByRole('alert').filter({ hasText: pattern })).toBeVisible();
  await assertInvoicePending(page, customerName);
}

test('Manager approval above 50,000 is rejected and invoice remains pending', async ({ page }) => {
  const customerName = `Manager Limit ${Date.now()}`;

  await createAndSubmitInvoice(page, 'demo.manager', customerName, '50001');
  await approveFromApprovals(page, customerName);

  const errorAlert = page.getByRole('alert').filter({
    hasText: /Manager approval is limited to invoices up to 50000/i,
  });
  await expect(errorAlert).toBeVisible();

  await assertInvoicePending(page, customerName);
});

test('Director approval for a >500,000 invoice requires a second stage and stays pending until final approval', async ({ page }) => {
  const customerName = `Director High Value ${Date.now()}`;

  await createAndSubmitInvoice(page, 'demo.director', customerName, '500001');
  await approveFromApprovals(page, customerName);

  await assertNoDecisionError(page);
  await assertInvoicePending(page, customerName);

  await page.getByRole('button', { name: 'Sign out' }).click();
  await signIn(page, 'demo.director2', 'ChangeMe123!');
  await approveFromApprovals(page, customerName);

  await page.getByRole('tab', { name: 'Invoices' }).click();
  await expect(page.getByRole('row').filter({ hasText: customerName })).toContainText('Approved');
});

test('High-value approvals reject same-user and non-Director second stage attempts without mutating invoice state', async ({ page }) => {
  const customerName = `Same User Rejection ${Date.now()}`;

  await createAndSubmitInvoice(page, 'demo.director', customerName, '600001');
  
  // Stage 1: Director approves, invoice must remain pending
  await approveFromApprovals(page, customerName);
  await assertInvoicePending(page, customerName);

  // Same Director tries stage 2, must be rejected
  await expectApprovalFailure(page, customerName, /distinct approver usernames|same user/i);

  await page.getByRole('button', { name: 'Sign out' }).click();

  // Manager tries stage 2, must be rejected
  await signIn(page, 'demo.manager', 'ChangeMe123!');
  await approveFromApprovals(page, customerName);

  await expect(page.getByRole('alert').filter({
    hasText: /second approver must be a Director|Director/i,
  })).toBeVisible();
  await assertInvoicePending(page, customerName);
});
