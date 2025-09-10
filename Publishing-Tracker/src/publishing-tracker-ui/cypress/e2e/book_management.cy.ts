/// <reference types="cypress" />

describe('Book Management', () => {
    beforeEach(() => {
        // For a real application, you would seed a test user in the database
        // and then programmatically log in. For this example, we'll use the UI.
        cy.visit('http://localhost:5173/login');
        cy.get('[data-cy="email-input"]').type('test@test.com');
        cy.get('[data-cy="password-input"]').type('password123');
        cy.get('[data-cy="login-button"]').click();
        cy.url().should('include', '/dashboard');
    });

    it('should allow a user to add a new book', () => {
        cy.visit('http://localhost:5173/books');
        cy.contains('Add New Book').click();

        const bookTitle = `Test Book ${Date.now()}`;
        cy.get('input[name="title"]').type(bookTitle);
        cy.get('input[name="author"]').type('Test Author');
        cy.get('button[type="submit"]').click();

        cy.url().should('include', '/books');
        cy.contains(bookTitle).should('be.visible');
    });
});