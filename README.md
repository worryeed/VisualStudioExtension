# AI Code Assistant для Visual Studio

## 📋 Описание  
Расширение для Visual Studio, помогающее в написании и улучшении кода с использованием нейросети. Позволяет ускорить разработку за счет интеллектуальных подсказок и автоматизации рутинных задач.

***

## 🛠️ Функционал  
- **Автозаполнение кода**: Подсказки и автозаполнение для ускоренного написания кода.
- **Документация (XML комментарии)**: Создание XML комментариев для методов и классов, улучшая читаемость кода.
- **Встроенный чат с нейросетью**: Возможность задать вопросы и получить ответы от нейросети прямо в редакторе.
- **История запросов**: Сохранение результатов в PostgreSQL  
- **Кэширование**: Повторное использование частых запросов через Redis  

***

## 🏗️ Архитектура  
```mermaid
---
config:
  theme: dark
  look: neo
  layout: dagre
---
flowchart TD
 subgraph VisualStudio["VisualStudio"]
        B{{"Backend API"}}
        A(["VS Extension"])
  end
 subgraph BackendLayer["BackendLayer"]
        C[("Redis Cache")]
        D[("PostgreSQL")]
        E("RabbitMQ")
  end
 subgraph AIProcessing["AIProcessing"]
        F(["Celery Worker"])
        G("Ollama Service")
        H(("CodeLlama Model"))
  end
    A --> B
    B --> C & D & E
    E --> F
    F --> G
    G --> H
    style B fill:#C8E6C9,stroke:#424242,stroke-width:2px,color:#000000
    style A fill:#E1BEE7,stroke:#424242,stroke-width:2px,color:#000000
    style C fill:#FFCDD2,stroke:#424242,stroke-width:2px,color:#000000
    style D fill:#BBDEFB,stroke:#424242,stroke-width:2px,color:#000000
    style E fill:#FFE0B2,stroke:#424242,stroke-width:2px,color:#000000
    style F fill:#FFF9C4,stroke:#424242,stroke-width:2px,color:#000000
    style G fill:#C8E6C9,stroke:#424242,stroke-width:2px,color:#000000
    style H fill:#BBDEFB,stroke:#424242,stroke-width:2px,color:#000000
    style VisualStudio fill:#FFFFFF,stroke:#000000,color:#000000
    style AIProcessing stroke:#000000,fill:#FFFFFF,color:#000000
    style BackendLayer stroke:#000000,fill:#FFFFFF,color:#000000
