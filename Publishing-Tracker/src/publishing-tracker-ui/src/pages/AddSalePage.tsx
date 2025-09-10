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
        bookId: 0,
        platformId: 0,
        saleDate: new Date(),
        quantity: 1,
        unitPrice: 0,
        royalty: 0
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
                setError('Failed to fetch books and platforms.');
            }
        };

        fetchData();
    }, []);

    const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
        const { name, value } = e.target;
        setSale(prevSale => ({
            ...prevSale,
            [name]: value
        }));
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await saleService.createSale(sale);
            navigate('/sales');
        } catch (err) {
            setError('Failed to create sale.');
        }
    };

    return (
        <div className="form-container">
            <h1>Add Manual Sale</h1>
            {error && <p style={{ color: 'red' }}>{error}</p>}
            <form onSubmit={handleSubmit}>
                <div className="input-group">
                    <label>Book</label>
                    <select name="bookId" value={sale.bookId} onChange={handleChange} required>
                        <option value="">Select a book</option>
                        {books.map(book => (
                            <option key={book.id} value={book.id}>{book.title}</option>
                        ))}
                    </select>
                </div>
                <div className="input-group">
                    <label>Platform</label>
                    <select name="platformId" value={sale.platformId} onChange={handleChange} required>
                        <option value="">Select a platform</option>
                        {platforms.map(platform => (
                            <option key={platform.id} value={platform.id}>{platform.name}</option>
                        ))}
                    </select>
                </div>
                <div className="input-group">
                    <label>Sale Date</label>
                    <input type="date" name="saleDate" value={sale.saleDate.toISOString().split('T')[0]} onChange={handleChange} required />
                </div>
                <div className="input-group">
                    <label>Quantity</label>
                    <input type="number" name="quantity" value={sale.quantity} onChange={handleChange} required inputMode="numeric" />
                </div>
                <div className="input-group">
                    <label>Unit Price</label>
                    <input type="number" name="unitPrice" value={sale.unitPrice} onChange={handleChange} required inputMode="decimal" />
                </div>
                <div className="input-group">
                    <label>Royalty</label>
                    <input type="number" name="royalty" value={sale.royalty} onChange={handleChange} required inputMode="decimal" />
                </div>
                <button type="submit">Add Sale</button>
            </form>
        </div>
    );
};

export default AddSalePage;