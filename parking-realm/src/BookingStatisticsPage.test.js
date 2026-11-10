import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import BookingStatisticsPage from './BookingStatisticsPage';
import { getBookingStatistics } from './api/booking';

jest.mock('./api/booking');

function submitRange() {
  fireEvent.change(screen.getByLabelText('Fra'), { target: { value: '2025-01-01T00:00' } });
  fireEvent.change(screen.getByLabelText('Til'), { target: { value: '2025-01-02T00:00' } });
  userEvent.click(screen.getByRole('button', { name: 'Hent statistik' }));
}

test('shows booking totals and daily pattern results', async () => {
  getBookingStatistics.mockResolvedValue({ ok: true, data: {
    total: 3, active: 2, cancelled: 1, hasData: true,
    days: [{ day: '2025-01-01T00:00:00', total: 3, active: 2, cancelled: 1 }],
  } });
  render(<BookingStatisticsPage />);
  submitRange();
  expect(await screen.findByText('Bookinger: 3')).toBeInTheDocument();
  expect(screen.getByRole('table')).toBeInTheDocument();
  expect(screen.getByText('2025-01-01')).toBeInTheDocument();
});

test('empty data is explicitly labelled and is not shown as a trend', async () => {
  getBookingStatistics.mockResolvedValue({ ok: true, data: { total: 0, active: 0, cancelled: 0, hasData: false, days: [] } });
  render(<BookingStatisticsPage />);
  submitRange();
  expect(await screen.findByText('Ingen bookingdata i perioden.')).toBeInTheDocument();
  expect(screen.queryByRole('table')).not.toBeInTheDocument();
});

test('reporting failure is not presented as valid zero counts', async () => {
  getBookingStatistics.mockResolvedValue({ ok: false, error: 'Statistik kunne ikke hentes.' });
  render(<BookingStatisticsPage />);
  submitRange();
  expect(await screen.findByRole('alert')).toHaveTextContent('Statistik kunne ikke hentes.');
  expect(screen.queryByText('Bookinger: 0')).not.toBeInTheDocument();
});
