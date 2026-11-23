import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import AdminDashboard from './AdminDashboard';
import { bookingRequest } from './api/booking';

jest.mock('./api/booking');

const report = {
  status: { capacity: 20, reserved: 2, hasData: false, occupied: null, available: null },
  bookings: { hasData: false, total: 0, active: 0, cancelled: 0, days: [], hours: [] },
  occupancy: { hasData: false, days: [] },
  attendance: { hasData: false, eligible: 1, attended: 0, noShows: 0, unknown: 1, percent: null, pending: [] },
  revenue: { isDemo: true, day: '2026-11-16T00:00:00Z', actualExample: 1250, expectedExample: 1800, todayBookings: 2 },
};

beforeEach(() => {
  jest.clearAllMocks();
  bookingRequest.mockResolvedValue({ ok: true, data: report });
});

test('unknown physical occupancy and attendance are not presented as zero and revenue is labeled fictional', async () => {
  render(<AdminDashboard />);
  expect(await screen.findByText(/Aktuel fysisk belægning er ukendt/)).toBeInTheDocument();
  expect(screen.getByText(/Fremmøde kan ikke beregnes/)).toBeInTheDocument();
  expect(screen.getByText(/Fiktive eksempelbeløb/)).toBeInTheDocument();
  expect(screen.getByText(/Ingen bookingdata i perioden/)).toBeInTheDocument();
  expect(screen.getByText(/Ingen belægningsdata i perioden/)).toBeInTheDocument();
});

test('records an explicitly entered occupancy count and refreshes the report', async () => {
  render(<AdminDashboard />);
  await screen.findByText(/Kapacitet: 20/);
  fireEvent.change(screen.getByLabelText('Observeret antal optagede pladser'), { target: { value: '0' } });
  fireEvent.click(screen.getByRole('button', { name: 'Gem observation' }));
  await waitFor(() => expect(bookingRequest).toHaveBeenCalledWith('/api/admin/dashboard/occupancy', expect.objectContaining({
    method: 'POST', body: JSON.stringify({ occupied: 0 }),
  })));
  await waitFor(() => expect(bookingRequest.mock.calls.filter(([path]) => path.includes('?')).length).toBe(2));
});

test('failed refresh removes stale reports and shows an error', async () => {
  render(<AdminDashboard />);
  await screen.findByText(/Kapacitet: 20/);
  bookingRequest.mockResolvedValue({ ok: false, error: 'Server unavailable' });
  fireEvent.click(screen.getByRole('button', { name: 'Opdater dashboard' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Dashboarddata kunne ikke vises');
  expect(screen.queryByText(/Kapacitet: 20/)).not.toBeInTheDocument();
});

test('refreshes every fifteen seconds and cleans up on unmount', async () => {
  jest.useFakeTimers();
  try {
    const { unmount } = render(<AdminDashboard />);
    await act(async () => {});
    expect(bookingRequest).toHaveBeenCalledTimes(1);
    await act(async () => { jest.advanceTimersByTime(15000); });
    expect(bookingRequest).toHaveBeenCalledTimes(2);
    unmount();
    await act(async () => { jest.advanceTimersByTime(15000); });
    expect(bookingRequest).toHaveBeenCalledTimes(2);
  } finally { jest.useRealTimers(); }
});
