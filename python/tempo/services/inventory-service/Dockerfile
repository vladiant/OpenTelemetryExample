FROM python:3.11-slim

WORKDIR /app

# Copy requirements and install dependencies
COPY shared/requirements.txt /app/requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

# Copy shared tracing module
COPY shared/tracing.py /app/shared/tracing.py

# Copy service code
COPY inventory-service/main.py /app/main.py

EXPOSE 8002

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8002"]
