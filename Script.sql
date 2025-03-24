-- public.test definition

-- Drop table

-- DROP TABLE public.test;

CREATE TABLE public.test (
                             id int4 GENERATED ALWAYS AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 2147483647 START 1 CACHE 1 NO CYCLE) NOT NULL,
                             testvalue varchar NULL,
                             datevalue timestamp NULL,
                             testid uuid NOT NULL,
                             CONSTRAINT test_pk PRIMARY KEY (id)
);
CREATE INDEX test_testid_idx ON public.test USING btree (testid);