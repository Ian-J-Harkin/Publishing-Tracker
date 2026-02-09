import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { BrowserRouter } from 'react-router-dom';
import ImportPage from './ImportPage';
import { importService } from '../services/importService';

jest.mock('../services/importService');
const mockedImportService = importService as jest.Mocked<typeof importService>;

describe('ImportPage', () => {
    beforeEach(() => {
        jest.clearAllMocks();
    });

    it('should render the upload step initially', () => {
        render(
            <BrowserRouter>
                <ImportPage />
            </BrowserRouter>
        );

        expect(screen.getAllByText(/Upload Sales Data/i).length).toBeGreaterThan(0);
        expect(screen.getByText(/Step 1: Upload Data/i)).toBeInTheDocument();
    });

    it('should display summary with correct numbers after successful import', async () => {
        const mockPreviewData = {
            fileName: 'test.csv',
            headers: ['Book Title', 'Platform', 'Sale Date', 'Quantity', 'Unit Price', 'Royalty', 'Currency', 'Order ID'],
            previewRows: [
                { 'Book Title': 'Test Book', 'Platform': 'Amazon KDP', 'Sale Date': '2024-01-01', 'Quantity': '5', 'Unit Price': '9.99', 'Royalty': '3.50', 'Currency': 'USD', 'Order ID': 'TEST-001' }
            ]
        };

        const mockImportResult = {
            id: 1,
            fileName: 'test.csv',
            status: 'Completed',
            startedAt: new Date('2024-01-01'),
            completedAt: new Date('2024-01-01'),
            recordsProcessed: 15,
            recordsSuccessful: 13,
            recordsFailed: 2,
            errorLog: 'Row 5: Duplicate order ID\nRow 8: Invalid quantity'
        };

        mockedImportService.uploadFile.mockResolvedValue(mockPreviewData);
        mockedImportService.processFile.mockResolvedValue(mockImportResult);

        render(
            <BrowserRouter>
                <ImportPage />
            </BrowserRouter>
        );

        // Simulate file upload
        const file = new File(['test content'], 'test.csv', { type: 'text/csv' });
        const fileInput = screen.getByLabelText(/browse files/i).closest('input') as HTMLInputElement;

        fireEvent.change(fileInput, { target: { files: [file] } });

        // Click upload button
        const uploadButton = screen.getByText(/Next: Map Columns/i);
        fireEvent.click(uploadButton);

        await waitFor(() => {
            expect(screen.getByText(/Step 2: Map Columns/i)).toBeInTheDocument();
        });

        // Click process button
        const processButton = screen.getByText(/Process File/i);
        fireEvent.click(processButton);

        // Wait for summary to appear
        await waitFor(() => {
            expect(screen.getByText(/Import Synchronized/i)).toBeInTheDocument();
        }, { timeout: 3000 });

        // Check that the numbers are displayed
        await waitFor(() => {
            // Look for the actual number values
            const successfulElements = screen.getAllByText('13');
            const failedElements = screen.getAllByText('2');

            expect(successfulElements.length).toBeGreaterThan(0);
            expect(failedElements.length).toBeGreaterThan(0);

            // Check for labels
            expect(screen.getByText(/Successful/i)).toBeInTheDocument();
            expect(screen.getByText(/Failed/i)).toBeInTheDocument();
            expect(screen.getByText(/Success Rate/i)).toBeInTheDocument();

            // Check for accuracy calculation (13/15 = 86.67% -> rounds to 87%)
            expect(screen.getByText(/87%/)).toBeInTheDocument();
        });

        // Verify error log is displayed
        expect(screen.getByText(/Diagnostic Audit Trail/i)).toBeInTheDocument();
        expect(screen.getByText(/Duplicate order ID/i)).toBeInTheDocument();
    });

    it('should handle all records failed scenario', async () => {
        const mockPreviewData = {
            fileName: 'test.csv',
            headers: ['Book Title', 'Platform'],
            previewRows: []
        };

        const mockImportResult = {
            id: 2,
            fileName: 'test.csv',
            status: 'Completed',
            startedAt: new Date('2024-01-01'),
            completedAt: new Date('2024-01-01'),
            recordsProcessed: 5,
            recordsSuccessful: 0,
            recordsFailed: 5,
            errorLog: 'All records had duplicate order IDs'
        };

        mockedImportService.uploadFile.mockResolvedValue(mockPreviewData);
        mockedImportService.processFile.mockResolvedValue(mockImportResult);

        render(
            <BrowserRouter>
                <ImportPage />
            </BrowserRouter>
        );

        const file = new File(['test'], 'test.csv', { type: 'text/csv' });
        const fileInput = screen.getByLabelText(/browse files/i).closest('input') as HTMLInputElement;
        fireEvent.change(fileInput, { target: { files: [file] } });

        const uploadButton = screen.getByText(/Next: Map Columns/i);
        fireEvent.click(uploadButton);

        await waitFor(() => {
            expect(screen.getByText(/Step 2: Map Columns/i)).toBeInTheDocument();
        });

        const processButton = screen.getByText(/Process File/i);
        fireEvent.click(processButton);

        await waitFor(() => {
            expect(screen.getByText(/Import Synchronized/i)).toBeInTheDocument();
        });

        // Should show 0 successful, 5 failed
        await waitFor(() => {
            expect(screen.getByText('0')).toBeInTheDocument();
            expect(screen.getByText('5')).toBeInTheDocument();
            expect(screen.getByText(/0%/)).toBeInTheDocument(); // 0% accuracy
        });
    });
});
