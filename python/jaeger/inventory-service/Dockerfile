FROM python:3.11-slim

WORKDIR /app

# Copy requirements
COPY requirements.txt .

# Install dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY main.py .

# Expose port (will be overridden by docker-compose)
EXPOSE 8000

# Run the application
CMD ["python", "main.py"]