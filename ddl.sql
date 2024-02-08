DROP TABLE IF EXISTS customer;
DROP TABLE IF EXISTS "transaction";

CREATE TABLE customer (
  id SERIAL PRIMARY KEY,
  name VARCHAR(64) NOT NULL,
  "limit" INTEGER NOT NULL,
  balance INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE "transaction"(
    id SERIAL PRIMARY KEY,
    "value"  INTEGER NOT NULL,
    "type" CHAR NOT NULL,
    "description" VARCHAR(64) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    customer_id INTEGER NOT NULL,
    FOREIGN KEY (customer_id) REFERENCES customer(id)
);

CREATE INDEX idx_customer_id ON customer(id);

CREATE INDEX idx_transaction_id ON "transaction"(id);
CREATE INDEX idx_transaction_customer_id ON "transaction"(customer_id);


INSERT INTO customer (name, "limit", balance)
VALUES
    ('o barato sai caro', 1000 * 100, 1000 * 100),
    ('zan corp ltda', 800 * 100, 800 * 100),
    ('les cruders', 10000 * 100, 10000 * 100),
    ('padaria joia de cocaia', 100000 * 100, 100000 * 100),
    ('kid mais', 5000 * 100, 5000 * 100);
