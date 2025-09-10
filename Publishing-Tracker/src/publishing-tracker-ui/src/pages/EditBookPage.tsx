import React, { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { bookService } from '../services/bookService';
import type { Book, UpdateBook } from '../types/book';

const EditBookPage = () => {
    const { id } = useParams<{ id: string }>();
    const [book, setBook] = useState<Book | null>(null);
    const [error, setError] = useState<string | null>(null);
    const navigate = useNavigate();

    useEffect(() => {
        const fetchBook = async () => {
            try {
                if (id) {
                    const bookData = await bookService.getBookById(parseInt(id, 10));
                    setBook(bookData);
                }
            } catch (err) {
                setError('Failed to fetch book.');
            }
        };

        fetchBook();
    }, [id]);

    const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        const { name, value } = e.target;
        if (book) {
            setBook({ ...book, [name]: value });
        }
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (book && id) {
            try {
                const updateBook: UpdateBook = {
                    title: book.title,
                    author: book.author,
                    isbn: book.isbn,
                    publicationDate: book.publicationDate,
                    basePrice: book.basePrice,
                    genre: book.genre,
                    description: book.description
                };
                await bookService.updateBook(parseInt(id, 10), updateBook);
                navigate('/books');
            } catch (err) {
                setError('Failed to update book.');
            }
        }
    };

    if (!book) {
        return <div>Loading...</div>;
    }

    return (
        <div className="form-container">
            <h1>Edit Book</h1>
            {error && <p style={{ color: 'red' }}>{error}</p>}
            <form onSubmit={handleSubmit}>
                <div className="input-group">
                    <label>Title</label>
                    <input type="text" name="title" value={book.title} onChange={handleChange} required />
                </div>
                <div className="input-group">
                    <label>Author</label>
                    <input type="text" name="author" value={book.author} onChange={handleChange} required />
                </div>
                <div className="input-group">
                    <label>ISBN</label>
                    <input type="text" name="isbn" value={book.isbn || ''} onChange={handleChange} />
                </div>
                <div className="input-group">
                    <label>Publication Date</label>
                    <input type="date" name="publicationDate" value={book.publicationDate ? new Date(book.publicationDate).toISOString().split('T')[0] : ''} onChange={handleChange} />
                </div>
                <div className="input-group">
                    <label>Base Price</label>
                    <input type="number" name="basePrice" value={book.basePrice || ''} onChange={handleChange} inputMode="decimal" />
                </div>
                <div className="input-group">
                    <label>Genre</label>
                    <input type="text" name="genre" value={book.genre || ''} onChange={handleChange} />
                </div>
                <div className="input-group">
                    <label>Description</label>
                    <textarea name="description" value={book.description || ''} onChange={handleChange} />
                </div>
                <button type="submit">Update Book</button>
            </form>
        </div>
    );
};

export default EditBookPage;