Here are the SQL commands to create the databases BEFORE running EF migrations:🗄️ SQL Server Database Setup - Complete GuideOption 1: Using SQL Server Management Studio (SSMS)Step 1: Connect to SQL Server

Open SQL Server Management Studio (SSMS)
Connect to your SQL Server instance (usually localhost or .)
Use Windows Authentication or SQL Server Authentication
Step 2: Create Databases - Run These SQL Commands


-- ====================================
-- Smart Delivery System - Database Setup
-- ====================================

-- Create AuthService Database
CREATE DATABASE SmartDelivery_AuthDb;
GO

-- Create RouteService Database
CREATE DATABASE SmartDelivery_RouteDb;
GO

-- Create PackageService Database
CREATE DATABASE SmartDelivery_PackageDb;
GO

-- Verify databases were created
SELECT name, database_id, create_date 
FROM sys.databases 
WHERE name LIKE 'SmartDelivery_%';
GO