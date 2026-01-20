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
                setError('Failed to fetch book data.');
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
                setError('Failed to update book details.');
            }
        }
    };

    if (!book && !error) return <div className="loading">Loading book details...</div>;

    return (
        <div style={pageStyles.wrapper}>
            <div style={pageStyles.header}>
                <h1>Edit Book</h1>
                <p style={{ color: 'var(--text-muted)' }}>Update the information for <strong>{book?.title}</strong></p>
            </div>

            <div className="card" style={pageStyles.formCard}>
                {error && <div style={pageStyles.errorBanner}>{error}</div>}
                
                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label>Book Title</label>
                        <input 
                            type="text" 
                            name="title" 
                            value={book?.title || ''} 
                            onChange={handleChange} 
                            required 
                        />
                    </div>

                    <div style={pageStyles.row}>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Author</label>
                            <input 
                                type="text" 
                                name="author" 
                                value={book?.author || ''} 
                                onChange={handleChange} 
                                required 
                            />
                        </div>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>ISBN</label>
                            <input 
                                type="text" 
                                name="isbn" 
                                value={book?.isbn || ''} 
                                onChange={handleChange} 
                            />
                        </div>
                    </div>

                    <div style={pageStyles.row}>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Publication Date</label>
                            <input 
                                type="date" 
                                name="publicationDate" 
                                value={book?.publicationDate ? new Date(book.publicationDate).toISOString().split('T')[0] : ''} 
                                onChange={handleChange} 
                            />
                        </div>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Base Price ($)</label>
                            <input 
                                type="number" 
                                name="basePrice" 
                                step="0.01"
                                value={book?.basePrice || ''} 
                                onChange={handleChange} 
                                inputMode="decimal" 
                            />
                        </div>
                    </div>

                    <div className="form-group">
                        <label>Genre</label>
                        <input 
                            type="text" 
                            name="genre" 
                            value={book?.genre || ''} 
                            onChange={handleChange} 
                        />
                    </div>

                    <div className="form-group">
                        <label>Description</label>
                        <textarea 
                            name="description" 
                            rows={5}
                            value={book?.description || ''} 
                            onChange={handleChange} 
                        />
                    </div>

                    <div style={pageStyles.actions}>
                        <button type="button" onClick={() => navigate('/books')} style={pageStyles.cancelBtn}>
                            Cancel
                        </button>
                        <button type="submit" className="btn-primary">
                            Save Changes
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

// Reusing the styles from AddBookPage for perfect consistency
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

export default EditBookPage;