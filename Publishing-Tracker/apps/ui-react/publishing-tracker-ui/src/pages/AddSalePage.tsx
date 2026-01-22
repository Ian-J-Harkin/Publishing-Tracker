import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { saleService } from '../services/saleService';
import { bookService } from '../services/bookService';
import { platformService } from '../services/platformService';
import { CreateSale } from '../types/sale';
import { Book } from '../types/book';
import { Platform } from '../types/platform';

const AddSalePage = () => {
    const [sale, setSale] = useState<CreateSale>({
        bookId: 0, // Will be validated before submission
        platformId: 0, // Will be validated before submission
        saleDate: new Date(),
        quantity: 1,
        unitPrice: 0.01, // Backend requires minimum 0.01
        royalty: 0.01 // Backend requires minimum 0.01
    });
    const [books, setBooks] = useState<Book[]>([]);
    const [platforms, setPlatforms] = useState<Platform[]>([]);
    const [error, setError] = useState<string | null>(null);
    const navigate = useNavigate();

    useEffect(() => {
        const fetchData = async () => {
            try {
                const booksData = await bookService.getBooks();
                const platformsData = await platformService.getPlatforms();
                setBooks(booksData);
                setPlatforms(platformsData);
            } catch (err) {
                setError('Failed to fetch required data for sales entry.');
            }
        };
        fetchData();
    }, []);

    const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
        const { name, value } = e.target;
        let processedValue: any = value;

        // Convert numeric fields to numbers
        if (name === 'bookId' || name === 'platformId' || name === 'quantity' || name === 'unitPrice' || name === 'royalty') {
            processedValue = Number(value);
        }
        // Convert date string to Date object for API submission
        else if (name === 'saleDate') {
            processedValue = new Date(value);
        }

        setSale(prevSale => ({
            ...prevSale,
            [name]: processedValue
        }));
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null); // Clear any previous errors

        // Validation for required fields
        if (!sale.bookId || sale.bookId === 0) {
            setError('Please select a book.');
            return;
        }
        
        if (!sale.platformId || sale.platformId === 0) {
            setError('Please select a platform.');
            return;
        }

        if (!sale.quantity || sale.quantity <= 0) {
            setError('Please enter a valid quantity.');
            return;
        }

        if (!sale.unitPrice || sale.unitPrice < 0.01) {
            setError('Unit price must be at least $0.01.');
            return;
        }

        if (!sale.royalty || sale.royalty < 0.01) {
            setError('Royalty must be at least $0.01.');
            return;
        }

        try {
            await saleService.createSale(sale);
            navigate('/sales');
        } catch (err: any) {
            console.error('Failed to create sale:', err);
            
            // Check if this is a backend serialization error but the sale was actually created
            if (err.response?.status === 500 &&
                err.response?.data?.includes?.('"id":') &&
                err.response?.data?.includes?.('error":"An unexpected error occurred')) {
                // Sale was created successfully but response serialization failed
                console.log('Sale created successfully despite response error - navigating to sales page');
                navigate('/sales');
                return;
            }
            
            // Provide more specific error messages based on the response
            if (err.response?.status === 400) {
                setError('Invalid sale data. Please check all fields and try again.');
            } else if (err.response?.status === 401) {
                setError('You are not authorized to create sales. Please log in again.');
            } else if (err.response?.status === 404) {
                setError('Selected book or platform not found. Please refresh and try again.');
            } else if (err.response?.data?.message) {
                setError(`Failed to log transaction: ${err.response.data.message}`);
            } else {
                setError('Failed to log transaction. Please check your internet connection and try again.');
            }
        }
    };

    return (
        <div style={pageStyles.wrapper}>
            <div style={pageStyles.header}>
                <h1>Add Manual Sale</h1>
                <p style={{ color: 'var(--text-muted)' }}>Log an individual transaction for your records.</p>
            </div>

            <div className="card" style={pageStyles.formCard}>
                {error && <div style={pageStyles.errorBanner}>{error}</div>}
                
                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label>Select Book</label>
                        <select name="bookId" value={sale.bookId || ''} onChange={handleChange} required>
                            <option value="">Choose a book...</option>
                            {books.map(book => (
                                <option key={book.id} value={book.id}>{book.title}</option>
                            ))}
                        </select>
                    </div>

                    <div style={pageStyles.row}>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Platform</label>
                            <select name="platformId" value={sale.platformId || ''} onChange={handleChange} required>
                                <option value="">Choose platform...</option>
                                {platforms.map(platform => (
                                    <option key={platform.id} value={platform.id}>{platform.name}</option>
                                ))}
                            </select>
                        </div>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Sale Date</label>
                            <input 
                                type="date" 
                                name="saleDate" 
                                value={sale.saleDate instanceof Date ? sale.saleDate.toISOString().split('T')[0] : sale.saleDate} 
                                onChange={handleChange} 
                                required 
                            />
                        </div>
                    </div>

                    <div style={pageStyles.row}>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Quantity</label>
                            <input 
                                type="number" 
                                name="quantity" 
                                value={sale.quantity} 
                                onChange={handleChange} 
                                required 
                                inputMode="numeric" 
                            />
                        </div>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Unit Price ($)</label>
                            <input
                                type="number"
                                step="0.01"
                                min="0.01"
                                name="unitPrice"
                                value={sale.unitPrice}
                                onChange={handleChange}
                                required
                                inputMode="decimal"
                            />
                        </div>
                    </div>

                    <div className="form-group">
                        <label>Total Royalty Earned ($)</label>
                        <input
                            type="number"
                            step="0.01"
                            min="0.01"
                            name="royalty"
                            value={sale.royalty}
                            onChange={handleChange}
                            required
                            inputMode="decimal"
                            placeholder="0.01"
                        />
                    </div>

                    <div style={pageStyles.actions}>
                        <button type="button" onClick={() => navigate('/sales')} style={pageStyles.cancelBtn}>
                            Cancel
                        </button>
                        <button type="submit" className="btn-primary">
                            Log Sale
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

// Maintained style object for consistency
const pageStyles = {
    wrapper: { maxWidth: '800px', margin: '0 auto' },
    header: { marginBottom: '2rem' },
    formCard: { padding: '2.5rem' },
    row: { display: 'flex', gap: '1.5rem' },
    errorBanner: {
        backgroundColor: '#fef2f2',
        color: 'var(--danger)',
        padding: '1rem',
        borderRadius: '8px',
        marginBottom: '1.5rem',
        border: '1px solid #fee2e2',
        fontSize: '0.9rem',
    },
    actions: {
        display: 'flex',
        justifyContent: 'flex-end',
        gap: '1rem',
        marginTop: '2rem',
        paddingTop: '1.5rem',
        borderTop: '1px solid var(--border)',
    },
    cancelBtn: {
        backgroundColor: 'transparent',
        border: 'none',
        color: 'var(--text-muted)',
        fontWeight: '600',
        cursor: 'pointer',
    }
};

export default AddSalePage;