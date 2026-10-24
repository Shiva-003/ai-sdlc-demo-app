import { expect, test } from '@playwright/test';

test('Manager approval above 50,000 is rejected and invoice remains pending', async ({ page }) => {
  const customerName = `Manager Limit ${Date.now()}`;
  const amount = '50001';

  await page.goto('/');

  await page.getByLabel('Username').fill('demo.manager');
  await page.getByLabel('Password').fill('ChangeMe123!');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await page.getByLabel('Customer name').fill(customerName);
  await page.getByLabel('Amount').fill(amount);
  await page.getByRole('button', { name: 'Create invoice' }).click();

  const invoiceRow = page.getByRole('row').filter({ hasText: customerName });
  await invoiceRow.getByRole('button', { name: /Submit invoice/i }).click();

  await page.getByRole('tab', { name: 'Approvals' }).click();
  const pendingApprovalItem = page.getByRole('listitem').filter({ hasText: customerName });
  await pendingApprovalItem.getByRole('button', { name: /Approve invoice/i }).click();

  const errorAlert = page.getByRole('alert').filter({
    hasText: /Manager approval is limited to invoices up to 50000/i,
  });
  await expect(errorAlert).toBeVisible();

  await page.getByRole('tab', { name: 'Invoices' }).click();
  await expect(page.getByRole('row').filter({ hasText: customerName })).toContainText('Pending Approval');
});
